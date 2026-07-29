// Ignore Spelling: jwt

using Microsoft.Extensions.Logging;
using RoyalIdentity.Contexts;
using RoyalIdentity.Contracts;
using RoyalIdentity.Contracts.Models;
using RoyalIdentity.Contracts.Storage;
using RoyalIdentity.Extensions;
using RoyalIdentity.Models.Scopes;
using RoyalIdentity.Models.Tokens;
using RoyalIdentity.Options;
using RoyalIdentity.Pipelines.Abstractions;
using RoyalIdentity.Security.Cryptography;
using RoyalIdentity.Utils;
using System.Security.Claims;

namespace RoyalIdentity.Handlers;

/// <summary>
/// Renews a grant from a refresh token. The steps are deliberately ordered (plan-data-operational-storage
/// Fase 5): materialization and validation, then the atomic transition, then the tolerance policy, and only
/// then issuance — so nothing irreversible is emitted before this request has actually won the token.
/// </summary>
public class RefreshTokenHandler : IHandler<RefreshTokenContext>
{
    private readonly ILogger logger;
    private readonly IStorage storage;
    private readonly ITokenFactory tokenFactory;
    private readonly TimeProvider clock;
    private readonly IJwtFactory jwtFactory;

    public RefreshTokenHandler(
        ILogger<RefreshTokenHandler> logger,
        IStorage storage,
        ITokenFactory tokenFactory,
        TimeProvider clock,
        IJwtFactory jwtFactory)
    {
        this.logger = logger;
        this.storage = storage;
        this.tokenFactory = tokenFactory;
        this.clock = clock;
        this.jwtFactory = jwtFactory;
    }

    public async Task Handle(RefreshTokenContext context, CancellationToken ct)
    {
        context.ClientParameters.AssertHasClient();
        context.RefreshParameters.AssertHasRefreshToken();
        var client = context.ClientParameters.Client;
        var refreshToken = context.RefreshParameters.RefreshToken;
        var resourceStore = storage.GetResourceStore(context.Realm);

        logger.LogDebug("Processing refresh token request.");

        /////////////////////////////////////
        // Atomic transition — before issuance
        /////////////////////////////////////

        // Nothing is emitted until this request owns the token. A conflict is never converted into a success:
        // only a rematerialized consumed state may then be submitted to the tolerance policy (DF12).
        //
        // From here on the rematerialized instance is the one that matters. The instance the pipeline loaded
        // still carries the state version from before the transition, and any later conditional write that used
        // it would compare against a version the database has already moved past.
        var wonToken = await TryWinTheTokenAsync(context, client, refreshToken, ct);
        if (wonToken is null)
            return;

        refreshToken = wonToken;

        /////////////////////////////////////
        // Access Token
        /////////////////////////////////////

        var resources = await ResolveEffectiveResourcesAsync(context, refreshToken, ct);
        if (resources is null)
            return;

        AccessToken newAccessToken;
        if (refreshToken.ClaimsMode is RefreshTokenClaimsMode.Snapshot)
        {
            logger.LogDebug("Renewing access token from the refresh token snapshot");
            newAccessToken = await IssueFromSnapshotAsync(context, client, refreshToken, resources, ct);
        }
        else
        {
            logger.LogDebug("Issuing access token from current claims");

            var request = new AccessTokenRequest()
            {
                HttpContext = context.HttpContext,
                User = refreshToken.CreatePrincipal(),
                Client = client,
                Resources = resources,
                IdentityType = IdentityProfileTypes.User,
            };

            newAccessToken = await tokenFactory.CreateAccessTokenAsync(request, ct);
        }

        var subject = newAccessToken.CreatePrincipal();

        /////////////////////////////////////
        // Identity Token
        /////////////////////////////////////

        IdentityToken? newIdentityToken = null;
        if (newAccessToken.Scopes.Any(scope => scope.Contains(Server.StandardScopes.OpenId)))
        {
            var identityResources = await resourceStore.FindRequestedResourcesAsync(
                newAccessToken.Scopes, newAccessToken.ResourceUris, true, ct);

            var idTokenRequest = new IdentityTokenRequest()
            {
                HttpContext = context.HttpContext,
                User = subject,
                Client = client,
                Resources = identityResources,
                // DF42: at_hash must cover the access token returned in this very response. Hashing the
                // previous one produced an identity token that did not match what the client received.
                AccessTokenToHash = newAccessToken.Token,
                // DF32: in Snapshot the whole response reproduces the grant. Letting the identity token consult
                // the provider would return current claims beside an access token built from the snapshot.
                SnapshotClaims = refreshToken.ClaimsMode is RefreshTokenClaimsMode.Snapshot
                    ? [.. refreshToken.IdentityTokenClaims]
                    : null,
            };

            newIdentityToken = await tokenFactory.CreateIdentityTokenAsync(idTokenRequest, ct);
        }

        /////////////////////////////////////
        // Refresh Token
        /////////////////////////////////////

        var newRefreshToken = await IssueRefreshTokenAsync(
            context, client, refreshToken, newAccessToken, newIdentityToken, subject, ct);

        context.Response = new Responses.TokenResponse(
            newAccessToken,
            newRefreshToken,
            newIdentityToken,
            newAccessToken.Scopes.ToSpaceSeparatedString());
    }

    /// <summary>
    /// Runs the conditional transition and decides whether this request may continue. Returns the token this
    /// request may work with — always the rematerialized state, so its version matches the database — or
    /// <c>null</c> after setting the error response.
    /// </summary>
    private async Task<RefreshToken?> TryWinTheTokenAsync(
        RefreshTokenContext context, Models.Client client, RefreshToken refreshToken, CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var transition = await storage.GetRefreshTokenStore(context.Realm)
            .TryConsumeAsync(refreshToken.Token, refreshToken.StateVersion, now, ct);

        if (transition.IsSuccess)
            return transition.Current ?? refreshToken;

        if (transition.Outcome is RefreshTokenTransitionOutcome.NotFound)
        {
            logger.LogWarning("The refresh token no longer exists.");
            context.InvalidGrant("Invalid refresh token");
            return null;
        }

        // The tolerance is a separate product policy (DF37), applied only to the rematerialized consumed state —
        // never to the instance this request already mutated, and never as a reward for losing the race.
        var current = transition.Current;
        if (current?.ConsumedTime is not null && IsWithinTolerance(client, current.ConsumedTime, now))
        {
            logger.LogDebug("Refresh token already consumed, accepted within the configured tolerance.");
            return current;
        }

        logger.LogWarning("Rejecting refresh token because it has been consumed already.");
        context.InvalidGrant("Refresh token has been consumed already.");
        return null;
    }

    private static bool IsWithinTolerance(Models.Client client, DateTime? consumedTime, DateTime now)
    {
        var tolerance = client.RefreshTokenPostConsumedTimeTolerance;

        if (tolerance == TimeSpan.MaxValue)
            return true;

        return tolerance != TimeSpan.Zero && !consumedTime.HasExceeded(tolerance, now);
    }

    /// <summary>
    /// <c>Snapshot</c> mode: the renewed token reproduces the claims the refresh token carries, without asking
    /// the claims provider again (DF32). Account, session, client, expiration and consumption were already
    /// validated by the pipeline and by the transition above.
    /// </summary>
    private async Task<AccessToken> IssueFromSnapshotAsync(
        RefreshTokenContext context,
        Models.Client client,
        RefreshToken refreshToken,
        RequestedResources resources,
        CancellationToken ct)
    {
        var jti = CryptoRandom.CreateUniqueId(16, OutputFormat.Hex);
        var issuer = context.HttpContext.GetServerIssuerUri(context.Realm.Options);

        var token = new AccessToken(
            client.Id,
            issuer,
            AccessTokenType.Jwt,
            clock.GetUtcNow().UtcDateTime,
            client.AccessTokenLifetime,
            jti,
            Oidc.Token.Response.BearerTokenType)
        {
            AllowedSigningAlgorithms = resources.ResolveAccessTokenSigningAlgorithms(client).Algorithms,
            RealmId = context.Realm.Id,
        };

        // The snapshot supplies the subject and profile claims; scopes come from what this renewal resolved, so
        // a narrowed request narrows the token.
        token.Claims.AddRange(refreshToken.Claims.Where(claim => claim.Type != Jwt.ClaimTypes.Scope));
        token.Claims.AddRange(resources.ToScopeClaims());
        if (client.IncludeJwtId)
            token.Claims.Add(new Claim(JwtRegisteredClaimNames.Jti, jti));
        token.Claims.Add(new Claim(
            JwtRegisteredClaimNames.Iat,
            clock.GetUtcNow().ToUnixTimeSeconds().ToString(),
            ClaimValueTypes.Integer64));

        foreach (var audience in resources.GetAudiences())
            token.Audiences.Add(audience);

        token.ResourceUris.AddRange(resources.ProtectedResources.Select(resource => resource.ResourceUri));

        if (resources.IsOpenId)
            token.Audiences.Add(client.Id);

        if (token.AccessTokenType == AccessTokenType.Jwt)
            await jwtFactory.CreateTokenAsync(context.Realm, token, ct);

        await storage.GetAccessTokenStore(context.Realm).StoreAsync(token, ct);

        return token;
    }

    private async Task<RefreshToken> IssueRefreshTokenAsync(
        RefreshTokenContext context,
        Models.Client client,
        RefreshToken refreshToken,
        AccessToken newAccessToken,
        IdentityToken? newIdentityToken,
        ClaimsPrincipal subject,
        CancellationToken ct)
    {
        if (client.RefreshTokenExpiration == Models.TokenExpiration.Sliding
            && client.RefreshTokenPostConsumedTimeTolerance == TimeSpan.MaxValue)
        {
            // The reusable token keeps its handle. DF41: no identifier of the new access token is written into
            // it — there is nothing to rewrite — and the update is conditional on the version this request
            // materialized, so a concurrent writer cannot be lost.
            logger.LogDebug("Updating Refresh Token");

            var updated = await storage.GetRefreshTokenStore(context.Realm)
                .TryUpdateAsync(refreshToken, refreshToken.StateVersion, ct);
            if (!updated.IsSuccess)
                logger.LogWarning("The reusable refresh token was moved concurrently; keeping the issued tokens.");

            return refreshToken;
        }

        var refreshTokenRequest = new RefreshTokenRequest()
        {
            HttpContext = context.HttpContext,
            Subject = subject,
            Client = client,
            AccessToken = newAccessToken,
            IdentityTokenClaims = newIdentityToken?.Claims,
        };

        return await tokenFactory.CreateRefreshTokenAsync(refreshTokenRequest, ct);
    }

    /// <summary>
    /// Resolves what this renewal may cover. The authorized set comes from the refresh token's own grant — never
    /// from the access token row issued with it (DF41) — and the request can only narrow it, never widen it
    /// (DF32).
    /// </summary>
    private async Task<RequestedResources?> ResolveEffectiveResourcesAsync(
        RefreshTokenContext context,
        RefreshToken refreshToken,
        CancellationToken ct)
    {
        var resolution = await storage.GetResourceStore(context.Realm).ResolveAuthorizedSubsetAsync(
            refreshToken.RequestedScopes,
            refreshToken.ResourceUris,
            context.RequestedResourceUris,
            true,
            ct);

        if (!resolution.IsSuccess)
        {
            logger.LogError(
                "Refresh token resource resolution rejected: {Error} {Detail}", resolution.Error, resolution.Detail);
            context.Error(resolution.Error!, resolution.ErrorDescription!);
            return null;
        }

        return resolution.Resources;
    }
}
