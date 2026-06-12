using Microsoft.Extensions.Logging;
using RoyalIdentity.Contexts;
using RoyalIdentity.Contexts.Items;
using RoyalIdentity.Contracts;
using RoyalIdentity.Contracts.Models;
using RoyalIdentity.Contracts.Storage;
using RoyalIdentity.Events;
using RoyalIdentity.Extensions;
using RoyalIdentity.Models.Scopes;
using RoyalIdentity.Models.Tokens;
using RoyalIdentity.Pipelines.Abstractions;
using TokenResponse = RoyalIdentity.Responses.TokenResponse;

namespace RoyalIdentity.Handlers;

public class AuthorizationCodeHandler : IHandler<AuthorizationCodeContext>
{
    private readonly ITokenFactory tokenFactory;
    private readonly IEventDispatcher eventDispatcher;
    private readonly IStorage storage;
    private readonly ILogger logger;

    public AuthorizationCodeHandler(
        ITokenFactory tokenFactory,
        IEventDispatcher eventDispatcher,
        IStorage storage,
        ILogger<AuthorizationCodeHandler> logger)
    {
        this.tokenFactory = tokenFactory;
        this.eventDispatcher = eventDispatcher;
        this.storage = storage;
        this.logger = logger;
    }

    public async Task Handle(AuthorizationCodeContext context, CancellationToken ct)
    {
        logger.LogDebug("Handle authorization code context start");

        context.CodeParameters.AssertHasCode();
        context.ClientParameters.AssertHasClient();
        var code = context.CodeParameters.AuthorizationCode;
        var client = context.ClientParameters.Client;
        var resources = await ResolveEffectiveResourcesAsync(context, code.Scopes, ct);
        if (resources is null)
            return;

        AccessToken accessToken;
        RefreshToken? refreshToken = null;
        IdentityToken? identityToken = null;
        AccessTokenIssuedEvent atEvent;
        RefreshTokenIssuedEvent? rtEvent = null;
        IdentityTokenIssuedEvent? idEvent = null;

        var accessTokenRequest = new AccessTokenRequest
        {
            HttpContext = context.HttpContext,
            User = code.Subject,
            Resources = resources,
            Client = client,
            Confirmation = context.ClientParameters.Confirmation,
            IdentityType = IdentityProfileTypes.User,
        };

        accessToken = await tokenFactory.CreateAccessTokenAsync(accessTokenRequest, ct);
        atEvent = new AccessTokenIssuedEvent(context, new Token(Oidc.Token.Types.AccessToken, accessToken.Token));

        logger.LogDebug("Access token issued");

        if (resources.OfflineAccess)
        {
            var refreshTokenRequest = new RefreshTokenRequest()
            {
                HttpContext = context.HttpContext,
                Subject = code.Subject,
                Client = client,
                AccessToken = accessToken
            };

            refreshToken = await tokenFactory.CreateRefreshTokenAsync(refreshTokenRequest, ct);
            rtEvent = new RefreshTokenIssuedEvent(context, new Token(Oidc.Token.Types.RefreshToken, refreshToken.Token));

            logger.LogDebug("Refresh token issued");
        }

        if (resources.IsOpenId)
        {
            var idTokenRequest = new IdentityTokenRequest()
            {
                HttpContext = context.HttpContext,
                User = code.Subject,
                Client = client,
                Resources = resources,
                Nonce = code.Nonce,
                AccessTokenToHash = accessToken.Token,
            };

            identityToken = await tokenFactory.CreateIdentityTokenAsync(idTokenRequest, ct);
            idEvent = new IdentityTokenIssuedEvent(context, new Token(Oidc.Token.Types.IdentityToken, identityToken.Token));

            logger.LogDebug("Identity token issued");
        }

        context.Response = new TokenResponse(
            accessToken, 
            refreshToken, 
            identityToken, 
            resources.RequestedScopeNames.ToSpaceSeparatedString());

        await eventDispatcher.DispatchAsync(atEvent, context.Realm);

        if (rtEvent is not null)
            await eventDispatcher.DispatchAsync(rtEvent, context.Realm);

        if (idEvent is not null)
            await eventDispatcher.DispatchAsync(idEvent, context.Realm);

        logger.LogDebug("Handle authorize code context finished");
    }

    private async Task<RequestedResources?> ResolveEffectiveResourcesAsync(
        AuthorizationCodeContext context,
        RequestedResources authorizedResources,
        CancellationToken ct)
    {
        if (context.RequestedResourceUris.Count is 0)
            return authorizedResources;

        var authorizedResourceUris = authorizedResources.ProtectedResources
            .Select(resource => resource.ResourceUri)
            .ToHashSet(StringComparer.Ordinal);

        var unauthorizedResourceUris = context.RequestedResourceUris
            .Where(uri => !authorizedResourceUris.Contains(uri))
            .ToArray();

        if (unauthorizedResourceUris.Length is not 0)
        {
            logger.LogError(
                "Authorization code resource subset contains unauthorized resources: {Resources}",
                string.Join(" ", unauthorizedResourceUris));
            context.Error(Oidc.Token.Errors.InvalidTarget, "resource indicators requested were not authorized");
            return null;
        }

        var resourceStore = storage.GetResourceStore(context.Realm);
        var resources = await resourceStore.FindRequestedResourcesAsync(
            authorizedResources.RequestedScopeNames,
            context.RequestedResourceUris,
            true,
            ct);

        if (resources.HasInvalidTargets)
        {
            logger.LogError(
                "Authorization code resource subset contains invalid resources: {Resources}",
                resources.GetInvalidTargets());
            context.Error(Oidc.Token.Errors.InvalidTarget, "resource indicators requested are invalid");
            return null;
        }

        if (!resources.IsValid)
        {
            logger.LogError(
                "Authorization code resources contain invalid scopes: {Scopes}",
                resources.GetInvalidScopes());
            context.Error(Oidc.Token.Errors.InvalidScope, "scopes requested are invalid or inactive");
            return null;
        }

        if (!HasScopeResourceCoherence(resources))
        {
            logger.LogError("Authorization code resource subset is not coherent with the authorized scopes");
            context.Error(Oidc.Token.Errors.InvalidTarget, "scope requires a matching resource indicator");
            return null;
        }

        return resources;
    }

    private static bool HasScopeResourceCoherence(RequestedResources resources)
    {
        if (resources.ProtectedResources.Count is 0)
            return true;

        return resources.Scopes.All(scope =>
        {
            var owner = resources.ResourceServers
                .FirstOrDefault(rs => rs.Scopes.Any(s => s.Name == scope.Name));

            return owner is null
                || owner.ProtectedResources.Count is 0
                || owner.ProtectedResources.Any(pr => resources.ProtectedResources.Any(rp => rp.ResourceUri == pr.ResourceUri));
        });
    }
}
