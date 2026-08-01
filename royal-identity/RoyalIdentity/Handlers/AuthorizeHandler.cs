using Microsoft.Extensions.Logging;
using RoyalIdentity.Contexts;
using RoyalIdentity.Contexts.Items;
using RoyalIdentity.Contracts;
using RoyalIdentity.Contracts.Models;
using RoyalIdentity.Events;
using RoyalIdentity.Pipelines.Abstractions;
using RoyalIdentity.Responses;

namespace RoyalIdentity.Handlers;

public class AuthorizeHandler : IHandler<AuthorizeContext>
{
    private readonly ICodeFactory codeFactory;
    private readonly ITokenFactory tokenFactory;
    private readonly ISessionStateGenerator sessionStateGenerator;
    private readonly IEventDispatcher eventDispatcher;
    private readonly ILogger logger;

    public AuthorizeHandler(
        ICodeFactory codeFactory,
        ITokenFactory tokenFactory,
        ISessionStateGenerator sessionStateGenerator,
        IEventDispatcher eventDispatcher, 
        ILogger<AuthorizeHandler> logger) 
    {
        this.codeFactory = codeFactory;
        this.tokenFactory = tokenFactory;
        this.sessionStateGenerator = sessionStateGenerator;
        this.eventDispatcher = eventDispatcher;
        this.logger = logger;
    }

    public async Task Handle(AuthorizeContext context, CancellationToken ct)
    {
        logger.LogDebug("Handle authorize context start");

        context.ClientParameters.AssertHasClient();
        context.AssertHasRedirectUri();
        context.AssertResourcesValidated();

        string? codeValue = null;
        string? accessTokenValue = null;
        string? identityTokenValue = null;
        CodeIssuedEvent? codeEvent = null;
        AccessTokenIssuedEvent? atEvent = null;
        IdentityTokenIssuedEvent? idEvent = null;

        if (context.ResponseTypes.Contains(Oidc.ResponseTypes.Code))
        {
            var code = await codeFactory.CreateCodeAsync(context, ct);
            codeValue = code.Code;
            var token = new Token(Oidc.Token.Types.Code, codeValue);
            codeEvent = new CodeIssuedEvent(context, token);

            logger.LogDebug("Code generated");
        }

        if (context.ResponseTypes.Contains(Oidc.ResponseTypes.Token))
        {
            var request = new AccessTokenRequest()
            {
                HttpContext = context.HttpContext,
                User = context.Subject,
                Client = context.ClientParameters.Client,
                Resources = context.Scopes,
                IdentityType = IdentityProfileTypes.User,
            };

            var accessToken = await tokenFactory.CreateAccessTokenAsync(request, ct);
            accessTokenValue = accessToken.Token;

            var token = new Token(Oidc.Token.Types.AccessToken, accessTokenValue);
            atEvent = new AccessTokenIssuedEvent(context, token);

            logger.LogDebug("Access Token generated");
        }

        if (context.ResponseTypes.Contains(Oidc.ResponseTypes.IdToken))
        {
            var tokenRequest = new IdentityTokenRequest
            {
                HttpContext = context.HttpContext,
                User = context.Subject,
                Client = context.ClientParameters.Client,
                Resources = context.Scopes,
                Nonce = context.Nonce,
                AccessTokenToHash = accessTokenValue,
                AuthorizationCodeToHash = codeValue,
                StateHash = context.StateHash,
            };

            var idToken = await tokenFactory.CreateIdentityTokenAsync(tokenRequest, ct);
            identityTokenValue = idToken.Token;

            var token = new Token(Oidc.Token.Types.IdentityToken, identityTokenValue);
            idEvent = new IdentityTokenIssuedEvent(context, token);

            logger.LogDebug("Identity Token generated");
        }

        context.Response = AuthorizeResponseFactory.Success(
            sessionStateGenerator,
            context,
            codeValue, 
            identityTokenValue, 
            accessTokenValue);

        logger.LogDebug("Authorize endpoint response generated:\n{Response}", context.Response);

        // events should only be dispatched after AuthorizeResponse has been created

        if (codeEvent is not null)
            await eventDispatcher.DispatchAsync(codeEvent, context.Realm);

        if (atEvent is not null)
            await eventDispatcher.DispatchAsync(atEvent, context.Realm);

        if (idEvent is not null)
            await eventDispatcher.DispatchAsync(idEvent, context.Realm);

        logger.LogDebug("Handle authorize context finished");
    }
}
