using Microsoft.Extensions.Logging;
using RoyalIdentity.Contexts;
using RoyalIdentity.Contexts.Items;
using RoyalIdentity.Contracts;
using RoyalIdentity.Events;
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

        }

        context.Response = new AuthorizeResponse(context, codeValue, sessionState);

        // events should only be dispatched after AuthorizeResponse has been created

        if (codeEvent is not null)
            await eventDispatcher.DispatchAsync(codeEvent);




        switch (context.GrantType)
        {
            case GrantType.AuthorizationCode:
                await HandleCodeFlow(context, ct);

                break;
            case GrantType.Hybrid:
                await HandleHybridFlow(context, ct);
                break;
            default:
                logger.LogError("Unsupported grant type: {GrantType}", context.GrantType);
                throw new InvalidOperationException("invalid grant type: " + context.GrantType);
        }


    }

    private async Task HandleHybridFlow(AuthorizeContext context, CancellationToken ct)
    {
        throw new NotImplementedException();
    }

    private async Task HandleCodeFlow(AuthorizeContext context, CancellationToken ct)
    {
        logger.LogDebug("Creating Authorization Code Flow response.");

        var code = await codeFactory.CreateCodeAsync(context, ct);
        var codeValue = await codeStore.StoreAuthorizationCodeAsync(code);
        await userSession.AddClientIdAsync(context.ClientId!);

        logger.LogDebug("Code issued for {ClientId} / {SubjectId}: {Code}", context.ClientId, context.Identity?.Name, codeValue);

        context.Response = new Responses.AuthorizeResponse(context, codeValue, code.SessionState);

        var token = new Token(ResponseTypes.Code, codeValue);
        context.Items.AddToken(token);

        var evt = new CodeIssuedEvent(context, token);
        await eventDispatcher.DispatchAsync(evt);
    }
}
