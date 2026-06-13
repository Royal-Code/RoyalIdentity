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
using RoyalIdentity.Utils;
using System.Security.Claims;

namespace RoyalIdentity.Handlers;

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
        var accessTokenStore = storage.GetAccessTokenStore(context.Realm);
        var refreshTokenStore = storage.GetRefreshTokenStore(context.Realm);

        logger.LogDebug("Processing refresh token request.");

        AccessToken newAccessToken;
        RefreshToken newRefreshToken;
        IdentityToken? newIdentityToken = null;

        /////////////////////////////////////
        // Access Token
        /////////////////////////////////////

        var accessToken = await accessTokenStore.GetAsync(refreshToken.AccessTokenId!, ct);
        if (accessToken is null)
        {
            logger.LogError("Access token not found: {AccessTokenId}", refreshToken.AccessTokenId);
            context.InvalidGrant("Invalid refresh token");
            return;
        }

        if (client.UpdateAccessTokenClaimsOnRefresh)
        {
            logger.LogDebug("Creating a new access token");

            var resources = await ResolveEffectiveResourcesAsync(context, accessToken, refreshToken, true, ct);
            if (resources is null)
                return;

            var request = new AccessTokenRequest()
            {
                HttpContext = context.HttpContext,
                User = accessToken.CreatePrincipal(),
                Client = client,
                Resources = resources,
                IdentityType = IdentityProfileTypes.User,
            };

            newAccessToken = await tokenFactory.CreateAccessTokenAsync(request, ct);
        }
        else if (context.RequestedResourceUris.Count is not 0)
        {
            logger.LogDebug("Creating a new access token with requested resource subset");

            var resources = await ResolveEffectiveResourcesAsync(context, accessToken, refreshToken, true, ct);
            if (resources is null)
                return;

            var request = new AccessTokenRequest()
            {
                HttpContext = context.HttpContext,
                User = accessToken.CreatePrincipal(),
                Client = client,
                Resources = resources,
                IdentityType = IdentityProfileTypes.User,
            };

            newAccessToken = await tokenFactory.CreateAccessTokenAsync(request, ct);
        }
        else
        {
            logger.LogDebug("Refreshing access token");

            var jti = CryptoRandom.CreateUniqueId(16, CryptoRandom.OutputFormat.Hex);

            newAccessToken = accessToken.Renew(
                jti,
                clock.GetUtcNow().DateTime,
                client.AccessTokenLifetime);

            if (newAccessToken.AccessTokenType == AccessTokenType.Jwt)
            {
                await jwtFactory.CreateTokenAsync(context.Realm, newAccessToken, ct);
            }

            await accessTokenStore.StoreAsync(newAccessToken, ct);
        }

        var subject = newAccessToken.CreatePrincipal();

        /////////////////////////////////////
        // Refresh Token
        /////////////////////////////////////

        if (refreshToken.ConsumedTime is null)
        {
            refreshToken.ConsumedTime = clock.GetUtcNow().DateTime;
            await refreshTokenStore.UpdateAsync(refreshToken, ct);
        }

        if (client.RefreshTokenExpiration == Models.TokenExpiration.Sliding
            && client.RefreshTokenPostConsumedTimeTolerance == TimeSpan.MaxValue)
        {
            // just updates the current token
            logger.LogDebug("Updating Refresh Token");

            newRefreshToken = refreshToken;
            newRefreshToken.Claims.RemoveWhere(c => c.Type == JwtRegisteredClaimNames.Jti);
            newRefreshToken.Claims.Add(new Claim(JwtRegisteredClaimNames.Jti, newAccessToken.Id));

            await refreshTokenStore.UpdateAsync(refreshToken, ct);
        }
        else
        {
            // cria um novo token

            var refreshTokenRequest = new RefreshTokenRequest()
            {
                HttpContext = context.HttpContext,
                Subject = subject,
                Client = client,
                AccessToken = newAccessToken
            };

            newRefreshToken = await tokenFactory.CreateRefreshTokenAsync(refreshTokenRequest, ct);
        }

        /////////////////////////////////////
        // Identity Token
        /////////////////////////////////////

        if (newAccessToken.Scopes.Any(scope => scope.Contains(Server.StandardScopes.OpenId)))
        {
            var scopes = newAccessToken.Scopes;
            var resources = await resourceStore.FindRequestedResourcesAsync(scopes, newAccessToken.ResourceUris, true, ct);

            var idTokenRequest = new IdentityTokenRequest()
            {
                HttpContext = context.HttpContext,
                User = subject,
                Client = client,
                Resources = resources,
                AccessTokenToHash = accessToken.Token,
            };

            newIdentityToken = await tokenFactory.CreateIdentityTokenAsync(idTokenRequest, ct);
        }

        context.Response = new Responses.TokenResponse(
            newAccessToken,
            newRefreshToken,
            newIdentityToken,
            newAccessToken.Scopes.ToSpaceSeparatedString());
    }

    private async Task<RequestedResources?> ResolveEffectiveResourcesAsync(
        RefreshTokenContext context,
        AccessToken accessToken,
        RefreshToken refreshToken,
        bool onlyEnabled,
        CancellationToken ct)
    {
        var authorizedResourceUris = accessToken.ResourceUris.Concat(refreshToken.ResourceUris);

        var resolution = await storage.GetResourceStore(context.Realm).ResolveAuthorizedSubsetAsync(
            accessToken.Scopes,
            authorizedResourceUris,
            context.RequestedResourceUris,
            onlyEnabled,
            ct);

        if (!resolution.IsSuccess)
        {
            logger.LogError("Refresh token resource resolution rejected: {Error} {Detail}", resolution.Error, resolution.Detail);
            context.Error(resolution.Error!, resolution.ErrorDescription!);
            return null;
        }

        return resolution.Resources;
    }
}
