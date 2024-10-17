using Microsoft.Extensions.Logging;
using RoyalIdentity.Contexts;
using RoyalIdentity.Contexts.Items;
using RoyalIdentity.Contracts;
using RoyalIdentity.Events;
using RoyalIdentity.Options;
using RoyalIdentity.Pipelines.Abstractions;
using RoyalIdentity.Responses;
using RoyalIdentity.Users;
using static RoyalIdentity.Options.OidcConstants;

namespace RoyalIdentity.Handlers;

public class AuthorizeContextHandler : IHandler<AuthorizeContext>
{
    private readonly ICodeFactory codeFactory;
    private readonly ITokenFactory tokenFactory;
    private readonly IEventDispatcher eventDispatcher;
    
    private readonly ILogger logger;

    public AuthorizeContextHandler(
        ICodeFactory codeFactory,
        IAuthorizationCodeStore codeStore,
        IEventDispatcher eventDispatcher, 
        IUserSession userSession,
        ILogger<AuthorizeContextHandler> logger) 
    {
        this.codeFactory = codeFactory;
        this.eventDispatcher = eventDispatcher;
        this.logger = logger;
    }

    public async Task Handle(AuthorizeContext context, CancellationToken ct)
    {
        string? codeValue = null;
        string? sessionState = null;
        CodeIssuedEvent? codeEvent = null;

        if (context.ResponseTypes.Contains(ResponseTypes.Code))
        {
            var code = await codeFactory.CreateCodeAsync(context, ct);
            
            var token = new Token(ResponseTypes.Code, code.Code);
            context.Items.AddToken(token);
            codeEvent = new CodeIssuedEvent(context, token);
        }

        if (context.ResponseTypes.Contains(ResponseTypes.Token))
        {
            var request = new AccessTokenRequest()
            {
                Context = context,
                Subject = context.Subject,
                Resources = context.Resources,
                Raw = context.Raw,
                Caller = ServerConstants.ProfileDataCallers.ClaimsProviderAccessToken
            };

            var accessToken = await tokenFactory.CreateAccessTokenAsync(request, ct);

            var token = new Token(ResponseTypes.Token, accessToken.Token);
            context.Items.AddToken(token);
        }

        if (context.ResponseTypes.Contains(ResponseTypes.IdToken))
        {

        }

        context.Response = new AuthorizeResponse(context, codeValue, sessionState);

        // events should only be dispatched after AuthorizeResponse has been created

        if (codeEvent is not null)
            await eventDispatcher.DispatchAsync(codeEvent);


    }
}
