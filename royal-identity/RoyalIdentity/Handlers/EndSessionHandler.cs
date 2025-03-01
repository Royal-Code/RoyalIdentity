using Microsoft.Extensions.Logging;
using RoyalIdentity.Contexts;
using RoyalIdentity.Contracts.Models.Messages;
using RoyalIdentity.Contracts.Storage;
using RoyalIdentity.Endpoints.Defaults;
using RoyalIdentity.Extensions;
using RoyalIdentity.Pipelines.Abstractions;

namespace RoyalIdentity.Handlers;

public class EndSessionHandler : IHandler<EndSessionContext>
{
    private readonly IStorage storage;
    private readonly IMessageStore messageStore;
    private readonly ILogger logger;

    public EndSessionHandler(IStorage storage, IMessageStore messageStore, ILogger<EndSessionHandler> logger)
    {
        this.storage = storage;
        this.messageStore = messageStore;
        this.logger = logger;
    }

    public async Task Handle(EndSessionContext context, CancellationToken ct)
    {
        logger.LogDebug("End session request received");

        var sid = context.IdToken?.Principal.GetSessionId() ?? context.Principal.GetSessionId();

        var canLogoutWithoutUserConfirmation = await CanLogoutWithoutUserConfirmation(context, sid, ct);

        var logoutMessage = new LogoutMessage()
        {
            RealmId = context.Realm.Id,
            SessionId = sid,
            ShowSignoutPrompt = !canLogoutWithoutUserConfirmation,
            PostLogoutRedirectUri = context.PostLogoutRedirectUri,
            ClientName = context.ClientParameters.Client?.Name,
            State = context.State,
            UiLocales = context.UiLocales
        };

        var messageId = await messageStore.WriteAsync(new Message<LogoutMessage>(logoutMessage), ct);

        var options = context.Options.ServerOptions;

        var redirect = options.UserInteraction.LogoutPath;

        if (redirect.IsLocalUrl())
            redirect = context.HttpContext.GetServerRelativeUrl(redirect)!;

        redirect = redirect.AddQueryString(options.UserInteraction.LogoutIdParameter, messageId);

        logger.LogDebug("Redirecting to {Redirect}", redirect);

        context.Response = ResponseHandler.Redirect(redirect);
    }

    private async ValueTask<bool> CanLogoutWithoutUserConfirmation(EndSessionContext context, string sid, CancellationToken ct)
    {
        if (context.IdToken is null || context.ClientParameters.Client is null)
            return false;

        if (!context.ClientParameters.Client.AllowLogoutWithoutUserConfirmation)
            return false;

        var userSessionStore = storage.GetUserSessionStore(context.Realm);
        var userSession = await userSessionStore.GetUserSessionAsync(sid, ct);
        if (userSession is null)
            return false;

        return userSession.Clients.Count == 1 && userSession.Clients[0] == context.ClientParameters.Client.Id;
    }
}
