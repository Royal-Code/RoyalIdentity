using Microsoft.Extensions.Options;
using RoyalIdentity.Contexts;
using RoyalIdentity.Contracts.Models.Messages;
using RoyalIdentity.Contracts.Storage;
using RoyalIdentity.Endpoints.Defaults;
using RoyalIdentity.Extensions;
using RoyalIdentity.Options;
using RoyalIdentity.Pipelines.Abstractions;
using RoyalIdentity.Users.Contracts;

namespace RoyalIdentity.Handlers;

public class EndSessionHandler : IHandler<EndSessionContext>
{
    private readonly IUserSessionStore userSessionStore;
    private readonly IMessageStore messageStore;
    private readonly ServerOptions options;

    public EndSessionHandler(
        IUserSessionStore userSessionStore,
        IMessageStore messageStore,
        IOptions<ServerOptions> options)
    {
        this.userSessionStore = userSessionStore;
        this.messageStore = messageStore;
        this.options = options.Value;
    }

    public async Task Handle(EndSessionContext context, CancellationToken ct)
    {
        var sid = context.IdToken?.Principal.GetSessionId() ?? context.Principal.GetSessionId();

        var canLogoutWithoutUserConfirmation = await CanLogoutWithoutUserConfirmation(context, sid, ct);

        var logoutMessage = new LogoutMessage()
        {
            SessionId = sid,
            ShowSignoutPrompt = !canLogoutWithoutUserConfirmation,
            PostLogoutRedirectUri = context.PostLogoutRedirectUri,
            ClientName = context.ClientParameters.Client?.Name,
            State = context.State,
            UiLocales = context.UiLocales
        };

        var messageId = await messageStore.WriteAsync(new Message<LogoutMessage>(logoutMessage), ct);

        var redirect = options.UserInteraction.LogoutUrl;

        if (redirect.IsLocalUrl())
            redirect = context.HttpContext.GetServerRelativeUrl(redirect)!;

        redirect = redirect.AddQueryString(options.UserInteraction.LogoutIdParameter, messageId);

        context.Response = ResponseHandler.Redirect(redirect);
    }

    private async ValueTask<bool> CanLogoutWithoutUserConfirmation(EndSessionContext context, string sid, CancellationToken ct)
    {
        if (context.IdToken is null || context.ClientParameters.Client is null)
            return false;

        if (!context.ClientParameters.Client.AllowLogoutWithoutUserConfirmation)
            return false;

        var userSession = await userSessionStore.GetUserSessionAsync(sid, ct);
        if (userSession is null)
            return false;

        return userSession.Clients.Count == 1 && userSession.Clients[0] == context.ClientParameters.Client.Id;
    }
}
