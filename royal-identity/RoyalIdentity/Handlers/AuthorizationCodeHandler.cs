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
        // No requested subset: use the resources authorized at the authorize step as-is.
        if (context.RequestedResourceUris.Count is 0)
            return authorizedResources;

        var resolution = await storage.GetResourceStore(context.Realm).ResolveAuthorizedSubsetAsync(
            authorizedResources.RequestedScopeNames,
            authorizedResources.ProtectedResources.Select(resource => resource.ResourceUri),
            context.RequestedResourceUris,
            true,
            ct);

        if (!resolution.IsSuccess)
        {
            logger.LogError("Authorization code resource subset rejected: {Error} {Detail}", resolution.Error, resolution.Detail);
            context.Error(resolution.Error!, resolution.ErrorDescription!);
            return null;
        }

        return resolution.Resources;
    }
}
