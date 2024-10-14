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
    private readonly IAuthorizationCodeStore codeStore;
    private readonly IEventDispatcher eventDispatcher;
    private readonly IUserSession userSession;
    private readonly ILogger logger;

    public AuthorizeContextHandler(
        ICodeFactory codeFactory,
        IAuthorizationCodeStore codeStore,
        IEventDispatcher eventDispatcher, 
        IUserSession userSession,
        ILogger<AuthorizeContextHandler> logger) 
    {
        this.codeFactory = codeFactory;
        this.codeStore = codeStore;
        this.eventDispatcher = eventDispatcher;
        this.userSession = userSession;
        this.logger = logger;
    }

    public async Task Handle(AuthorizeContext context, CancellationToken ct)
    {
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
        if (context.ResponseType)

        throw new NotImplementedException();
    }

    private async Task HandleCodeFlow(AuthorizeContext context, CancellationToken ct)
    {
        logger.LogDebug("Creating Authorization Code Flow response.");

        var code = await codeFactory.CreateCodeAsync(context, ct);
        var codeValue = await codeStore.StoreAuthorizationCodeAsync(code);
        await userSession.AddClientIdAsync(context.ClientId!);

        logger.LogDebug("Code issued for {ClientId} / {SubjectId}: {Code}", context.ClientId, context.Identity?.Name, codeValue);

        context.Response = new AuthorizationCodeResponse(context, codeValue, code.SessionState);

        var token = new Token(ResponseTypes.Code, codeValue);
        context.Items.AddToken(token);

        var evt = new CodeIssuedEvent(context, token);
        await eventDispatcher.DispatchAsync(evt);
    }
}
