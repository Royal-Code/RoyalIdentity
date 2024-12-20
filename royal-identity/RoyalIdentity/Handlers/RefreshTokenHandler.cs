using Microsoft.Extensions.Logging;
using RoyalIdentity.Contexts;
using RoyalIdentity.Contracts;
using RoyalIdentity.Contracts.Models;
using RoyalIdentity.Contracts.Storage;
using RoyalIdentity.Extensions;
using RoyalIdentity.Models.Tokens;
using RoyalIdentity.Options;
using RoyalIdentity.Pipelines.Abstractions;
using RoyalIdentity.Utils;
using System.Security.Claims;
using static RoyalIdentity.Options.OidcConstants;

namespace RoyalIdentity.Handlers;

public class RefreshTokenHandler : IHandler<RefreshTokenContext>
{
    private readonly ILogger logger;
    private readonly IAccessTokenStore accessTokenStore;
    private readonly IResourceStore resourceStore;
    private readonly ITokenFactory tokenFactory;
    private readonly TimeProvider clock;
    private readonly IJwtFactory jwtFactory;
    private readonly IRefreshTokenStore refreshTokenStore;

    public RefreshTokenHandler(
        ILogger<RefreshTokenHandler> logger,
        IAccessTokenStore accessTokenStore,
        IResourceStore resourceStore,
        ITokenFactory tokenFactory,
        TimeProvider clock,
        IJwtFactory jwtFactory,
        IRefreshTokenStore refreshTokenStore)
    {
        this.logger = logger;
        this.accessTokenStore = accessTokenStore;
        this.resourceStore = resourceStore;
        this.tokenFactory = tokenFactory;
        this.clock = clock;
        this.jwtFactory = jwtFactory;
        this.refreshTokenStore = refreshTokenStore;
    }

    public async Task Handle(RefreshTokenContext context, CancellationToken ct)
    {
        context.AssertHasClient();
        context.AssertHasRefreshToken();

        logger.LogDebug("Processing refresh token request.");

        AccessToken newAccessToken;
        RefreshToken newRefreshToken;
        IdentityToken? newIdentityToken = null;

        /////////////////////////////////////
        // Access Token
        /////////////////////////////////////

        var accessToken = await accessTokenStore.GetAsync(context.RefreshToken.AccessTokenId!, ct);
        if (accessToken is null)
        {
            logger.LogError("Access token not found: {AccessTokenId}", context.RefreshToken.AccessTokenId);
            context.Error(TokenErrors.InvalidGrant, "Invalid refresh token");
            return;
        }

        if (context.Client.UpdateAccessTokenClaimsOnRefresh)
        {
            logger.LogDebug("Creating a new access token");

            var scopes = accessToken.Scopes;
            var resources = await resourceStore.FindResourcesByScopeAsync(scopes, true, ct);

            var request = new AccessTokenRequest()
            {
                Context = context,
                Raw = context.Raw,
                User = context.GetSubject()!,
                Client = context.Client,
                Resources = resources,
                Caller = nameof(RefreshTokenHandler),
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
                context.Client.AccessTokenLifetime);

            if (newAccessToken.AccessTokenType == AccessTokenType.Jwt)
            {
                await jwtFactory.CreateTokenAsync(newAccessToken, ct);
            }

            await accessTokenStore.StoreAsync(newAccessToken, ct);
        }

        var subject = newAccessToken.CreatePrincipal();

        /////////////////////////////////////
        // Refresh Token
        /////////////////////////////////////

        if (context.RefreshToken.ConsumedTime is null)
        {
            context.RefreshToken.ConsumedTime = clock.GetUtcNow().DateTime;
            await refreshTokenStore.UpdateAsync(context.RefreshToken, ct);
        }

        if (context.Client.RefreshTokenExpiration == Models.TokenExpiration.Sliding
            && context.Client.RefreshTokenPostConsumedTimeTolerance == TimeSpan.MaxValue)
        {
            // just updates the current token
            logger.LogDebug("Updating Refresh Token");

            newRefreshToken = context.RefreshToken;
            newRefreshToken.Claims.RemoveWhere(c => c.Type == JwtClaimTypes.JwtId);
            newRefreshToken.Claims.Add(new Claim(JwtClaimTypes.JwtId, accessToken.Id));

            await refreshTokenStore.UpdateAsync(context.RefreshToken, ct);
        }
        else
        {
            // cria um novo token

            var refreshTokenRequest = new RefreshTokenRequest()
            {
                Context = context,
                Raw = context.Raw,
                Subject = subject,
                AccessToken = accessToken,
                Caller = nameof(AuthorizationCodeHandler)
            };

            newRefreshToken = await tokenFactory.CreateRefreshTokenAsync(refreshTokenRequest, ct);
        }

        /////////////////////////////////////
        // Identity Token
        /////////////////////////////////////

        if (newAccessToken.Scopes.Any(scope => scope.Contains(StandardScopes.OpenId)))
        {
            var scopes = newAccessToken.Scopes;
            var resources = await resourceStore.FindResourcesByScopeAsync(scopes, true, ct);

            var idTokenRequest = new IdentityTokenRequest()
            {
                Context = context,
                Raw = context.Raw,
                Subject = subject,
                Resources = resources,
                Caller = nameof(AuthorizationCodeHandler),
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
}
