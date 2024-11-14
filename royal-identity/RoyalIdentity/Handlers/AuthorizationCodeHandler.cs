using Microsoft.Extensions.Logging;
using RoyalIdentity.Contexts;
using RoyalIdentity.Contexts.Items;
using RoyalIdentity.Contracts;
using RoyalIdentity.Contracts.Models;
using RoyalIdentity.Events;
using RoyalIdentity.Extensions;
using RoyalIdentity.Models.Tokens;
using RoyalIdentity.Pipelines.Abstractions;
using static RoyalIdentity.Options.OidcConstants;
using TokenResponse = RoyalIdentity.Responses.TokenResponse;

namespace RoyalIdentity.Handlers;

public class AuthorizationCodeHandler : IHandler<AuthorizationCodeContext>
{
    private readonly ITokenFactory tokenFactory;
    private readonly IEventDispatcher eventDispatcher;
    private readonly ILogger logger;

    public AuthorizationCodeHandler(
        ITokenFactory tokenFactory,
        IEventDispatcher eventDispatcher,
        ILogger<AuthorizationCodeHandler> logger)
    {
        this.tokenFactory = tokenFactory;
        this.eventDispatcher = eventDispatcher;
        this.logger = logger;
    }

    public async Task Handle(AuthorizationCodeContext context, CancellationToken ct)
    {
        logger.LogDebug("Handle authorization code context start");

        context.AssertHasCode();
        context.AssertHasClient();

        AccessToken accessToken;
        RefreshToken? refreshToken = null;
        IdentityToken? identityToken = null;
        AccessTokenIssuedEvent atEvent;
        RefreshTokenIssuedEvent? rtEvent = null;
        IdentityTokenIssuedEvent? idEvent = null;

        var accessTokenRequest = new AccessTokenRequest
        {
            Context = context,
            Raw = context.Raw,
            Subject = context.AuthorizationCode.Subject,
            Resources = context.Resources,
            Confirmation = context.ClientSecret.Confirmation,
            Caller = context.GrantType
        };

        accessToken = await tokenFactory.CreateAccessTokenAsync(accessTokenRequest, ct);
        atEvent = new AccessTokenIssuedEvent(context, new Token(TokenTypes.AccessToken, accessToken.Token));

        logger.LogDebug("Access token issued");


        if (context.Resources.OfflineAccess)
        {
            var refreshTokenRequest = new RefreshTokenRequest()
            {
                Context = context,
                Raw = context.Raw,
                Subject = context.AuthorizationCode.Subject,
                AccessToken = accessToken,
                Caller = context.GrantType
            };

            refreshToken = await tokenFactory.CreateRefreshTokenAsync(refreshTokenRequest, ct);
            rtEvent = new RefreshTokenIssuedEvent(context, new Token(TokenTypes.RefreshToken, refreshToken.Token));

            logger.LogDebug("Refresh token issued");
        }

        if (context.AuthorizationCode.IsOpenId)
        {
            var idTokenRequest = new IdentityTokenRequest()
            {
                Context = context,
                Raw = context.Raw,
                Subject = context.AuthorizationCode.Subject,
                Resources = context.Resources,
                Caller = context.GrantType,
                Nonce = context.AuthorizationCode.Nonce,
                AccessTokenToHash = accessToken.Token,
            };

            identityToken = await tokenFactory.CreateIdentityTokenAsync(idTokenRequest, ct);
            idEvent = new IdentityTokenIssuedEvent(context, new Token(TokenTypes.IdentityToken, identityToken.Token));

            logger.LogDebug("Identity token issued");
        }

        context.Response = new TokenResponse(
            accessToken, 
            refreshToken, 
            identityToken, 
            context.AuthorizationCode.RequestedScopes.ToSpaceSeparatedString());

        await eventDispatcher.DispatchAsync(atEvent);

        if (rtEvent is not null)
            await eventDispatcher.DispatchAsync(rtEvent);

        if (idEvent is not null)
            await eventDispatcher.DispatchAsync(idEvent);

        logger.LogDebug("Handle authorize code context finished");
    }
}
