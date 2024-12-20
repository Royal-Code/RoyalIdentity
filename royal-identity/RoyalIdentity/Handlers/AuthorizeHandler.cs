using Microsoft.Extensions.Logging;
using RoyalIdentity.Contexts;
using RoyalIdentity.Contexts.Items;
using RoyalIdentity.Contracts;
using RoyalIdentity.Contracts.Models;
using RoyalIdentity.Events;
using RoyalIdentity.Models.Tokens;
using RoyalIdentity.Options;
using RoyalIdentity.Pipelines.Abstractions;
using RoyalIdentity.Responses;
using static RoyalIdentity.Options.OidcConstants;

namespace RoyalIdentity.Handlers;

public class AuthorizeHandler : IHandler<AuthorizeContext>
{
    private readonly ICodeFactory codeFactory;
    private readonly ITokenFactory tokenFactory;
    private readonly IEventDispatcher eventDispatcher;
    private readonly ILogger logger;

    public AuthorizeHandler(
        ICodeFactory codeFactory,
        ITokenFactory tokenFactory,
        IEventDispatcher eventDispatcher, 
        ILogger<AuthorizeHandler> logger) 
    {
        this.codeFactory = codeFactory;
        this.tokenFactory = tokenFactory;
        this.eventDispatcher = eventDispatcher;
        this.logger = logger;
    }

    public async Task Handle(AuthorizeContext context, CancellationToken ct)
    {
        logger.LogDebug("Handle authorize context start");

        context.AssertHasClient();

        string? codeValue = null;
        string? sessionState = null;
        string? accessTokenValue = null;
        string? identityTokenValue = null;
        CodeIssuedEvent? codeEvent = null;
        AccessTokenIssuedEvent? atEvent = null;
        IdentityTokenIssuedEvent? idEvent = null;

        if (context.ResponseTypes.Contains(ResponseTypes.Code))
        {
            var code = await codeFactory.CreateCodeAsync(context, ct);
            codeValue = code.Code;
            sessionState = code.SessionState;

            var token = new Token(TokenTypes.Code, codeValue);
            context.Items.AddToken(token);
            codeEvent = new CodeIssuedEvent(context, token);

            logger.LogDebug("Code generated");
        }

        if (context.ResponseTypes.Contains(ResponseTypes.Token))
        {
            var request = new AccessTokenRequest()
            {
                Context = context,
                Raw = context.Raw,
                User = context.Subject,
                Client = context.Client,
                Resources = context.Resources,
                Caller = ServerConstants.ProfileDataCallers.ClaimsProviderAccessToken
            };

            var accessToken = await tokenFactory.CreateAccessTokenAsync(request, ct);
            accessTokenValue = accessToken.Token;

            var token = new Token(TokenTypes.AccessToken, accessTokenValue);
            context.Items.AddToken(token);

            atEvent = new AccessTokenIssuedEvent(context, token);

            logger.LogDebug("Access Token generated");
        }

        if (context.ResponseTypes.Contains(ResponseTypes.IdToken))
        {
            var tokenRequest = new IdentityTokenRequest
            {
                Context = context,
                Subject = context.Subject,
                Resources = context.Resources,
                Raw = context.Raw,
                Caller = ServerConstants.ProfileDataCallers.ClaimsProviderAccessToken,
                Nonce = context.Nonce,
                AccessTokenToHash = accessTokenValue,
                AuthorizationCodeToHash = codeValue,
                StateHash = context.StateHash,
            };

            var idToken = await tokenFactory.CreateIdentityTokenAsync(tokenRequest, ct);
            identityTokenValue = idToken.Token;

            var token = new Token(TokenTypes.IdentityToken, identityTokenValue);
            context.Items.AddToken(token);

            idEvent = new IdentityTokenIssuedEvent(context, token);

            logger.LogDebug("Identity Token generated");
        }

        context.Response = new AuthorizeResponse(
            context,
            codeValue, 
            sessionState, 
            identityTokenValue, 
            accessTokenValue);

        logger.LogDebug("Authorize endpoint response generated:\n{Response}", context.Response);

        // events should only be dispatched after AuthorizeResponse has been created

        if (codeEvent is not null)
            await eventDispatcher.DispatchAsync(codeEvent);

        if (atEvent is not null)
            await eventDispatcher.DispatchAsync(atEvent);

        if (idEvent is not null)
            await eventDispatcher.DispatchAsync(idEvent);

        logger.LogDebug("Handle authorize context finished");
    }
}
