using Microsoft.Extensions.Logging;
using RoyalIdentity.Contexts;
using RoyalIdentity.Contexts.Items;
using RoyalIdentity.Contracts;
using RoyalIdentity.Events;
using RoyalIdentity.Pipelines.Abstractions;
using RoyalIdentity.Responses;

namespace RoyalIdentity.Handlers;

public class AuthorizeHandler : IHandler<AuthorizeContext>
{
    private readonly ICodeFactory codeFactory;
    private readonly ISessionStateGenerator sessionStateGenerator;
    private readonly IEventDispatcher eventDispatcher;
    private readonly ILogger logger;

    public AuthorizeHandler(
        ICodeFactory codeFactory,
        ISessionStateGenerator sessionStateGenerator,
        IEventDispatcher eventDispatcher, 
        ILogger<AuthorizeHandler> logger) 
    {
        this.codeFactory = codeFactory;
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

        var code = await codeFactory.CreateCodeAsync(context, ct);
        var codeValue = code.Code;
        var codeEvent = new CodeIssuedEvent(context, new Token(Oidc.Token.Types.Code, codeValue));

        logger.LogDebug("Code generated");

        context.Response = AuthorizeResponseFactory.Success(
            sessionStateGenerator,
            context,
            codeValue);

        logger.LogDebug("Authorize endpoint response generated:\n{Response}", context.Response);

        // events should only be dispatched after AuthorizeResponse has been created

        await eventDispatcher.DispatchAsync(codeEvent, context.Realm);

        logger.LogDebug("Handle authorize context finished");
    }
}
