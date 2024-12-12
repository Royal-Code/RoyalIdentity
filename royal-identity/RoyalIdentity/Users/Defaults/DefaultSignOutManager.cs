using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using RoyalIdentity.Options;
using RoyalIdentity.Users.Contracts;
using RoyalIdentity.Extensions;
using RoyalIdentity.Contracts;
using RoyalIdentity.Events;
using Microsoft.AspNetCore.Authentication;
using RoyalIdentity.Contracts.Storage;
using RoyalIdentity.Contracts.Models.Messages;
using Microsoft.Extensions.Options;
using RoyalIdentity.Contracts.Models;

namespace RoyalIdentity.Users.Defaults;

public class DefaultSignOutManager : ISignOutManager
{
    private readonly IHttpContextAccessor httpContextAccessor;
    private readonly IUserSessionStore userSessionStore;
    private readonly IEventDispatcher eventDispatcher;
    private readonly IClientStore clients;
    private readonly IBackChannelLogoutNotifier backChannelNotifier;
    private readonly IMessageStore messageStore;
    private readonly AccountOptions accountOptions;
    private readonly ServerOptions serverOptions;
    private readonly ILogger logger;

    public DefaultSignOutManager(
        IHttpContextAccessor httpContextAccessor,
        IUserSessionStore userSessionStore,
        IEventDispatcher eventDispatcher,
        IClientStore clients,
        IBackChannelLogoutNotifier backChannelNotifier,
        IMessageStore messageStore,
        IOptions<AccountOptions> accountOptions,
        IOptions<ServerOptions> serverOptions,
        ILogger<DefaultSignOutManager> logger)
    {
        this.httpContextAccessor = httpContextAccessor;
        this.userSessionStore = userSessionStore;
        this.eventDispatcher = eventDispatcher;
        this.backChannelNotifier = backChannelNotifier;
        this.clients = clients;
        this.messageStore = messageStore;
        this.accountOptions = accountOptions.Value;
        this.serverOptions = serverOptions.Value;
        this.logger = logger;
    }

    public async Task<string?> CreateLogoutIdAsync(CancellationToken ct)
    {
        var httpContext = httpContextAccessor.HttpContext;
        if (httpContext is null || httpContext.User.Identity is null || !httpContext.User.Identity.IsAuthenticated)
        {
            return null;
        }

        var identity = httpContext.User.Identity;
        var sid = identity.GetSessionId();

        LogoutMessage message = new LogoutMessage()
        {
            SessionId = sid,
            ShowSignoutPrompt = true,
        };

        return await messageStore.WriteAsync<LogoutMessage>(new(message), ct);
    }

    public async Task<Uri> SignOutAsync(LogoutMessage message, CancellationToken ct)
    {
        var sessionId = message.SessionId;
        var postLogoutRedirectUri = message.PostLogoutRedirectUri;
        var state = message.State;

        logger.LogDebug("Start Sign Out, session: {SessionId}, post logout uri: {Uri}", sessionId, postLogoutRedirectUri);

        var session = await userSessionStore.EndSessionAsync(sessionId, ct);
        
        if (session is null)
        {
            logger.LogWarning("User session not found, session id: {SessionId}", sessionId);
        }

        var httpContext = httpContextAccessor.HttpContext;
        if (httpContext is not null && httpContext.User.IsAuthenticated())
        {
            // delete local authentication cookie
            await httpContext.SignOutAsync();

            // raise the logout event
            await eventDispatcher.DispatchAsync(
                new UserLogoutSuccessEvent(httpContext.User.GetSubjectId(), session?.Id));

            logger.LogDebug("User logout sucess");
        }
        else
        {
            logger.LogDebug("User not authenticated");
        }

        HashSet<string> frontChannelLogout = [];
        HashSet<LogoutBackChannelRequest> backChannelLogout = [];

        if (session is not null && httpContext is not null)
        {
            string? iss = null;

            foreach(var clientId in session.Clients)
            {
                var client = await clients.FindClientByIdAsync(clientId, ct);
                if (client is null || !client.Enabled)
                    continue;

                foreach(var uri in client.FrontChannelLogoutUri)
                {
                    iss ??= httpContext.GetServerIssuerUri();

                    var logoutUri = client.FrontChannelLogoutSessionRequired
                        ? uri.AddQueryString(JwtClaimTypes.Issuer, iss).AddQueryString(JwtClaimTypes.SessionId, session.Id)
                        : uri;
                    
                    frontChannelLogout.Add(logoutUri);
                }

                foreach (var uri in client.BackChannelLogoutUri)
                {
                    iss ??= httpContext.GetServerIssuerUri();

                    backChannelLogout.Add(new LogoutBackChannelRequest()
                    {
                        ClientId = clientId,
                        Issuer = iss,
                        Subject = session.Username,
                        SessionId = session.Id,
                        Audience = clientId,
                        Uri = uri,
                        RequireSessionId = client.BackChannelLogoutSessionRequired
                    });
                }
            }
        }

        foreach(var backChannelRequest in backChannelLogout)
        {
            await backChannelNotifier.SendAsync(backChannelRequest, ct);
        }

        // determine if it can directly redirect to post logout uri
        // must have a post logout uri, no front channel logouts, and automatic redirect after signout
        var canRedirectToPostLogoutUri = postLogoutRedirectUri is not null &&
            frontChannelLogout.Count == 0 &&
            accountOptions.AutomaticRedirectAfterSignOut;

        if (canRedirectToPostLogoutUri)
        {
            var redirectUri = postLogoutRedirectUri!;

            if (state.IsPresent())
                redirectUri = redirectUri.AddQueryString(OidcConstants.EndSessionRequest.State, state);

            return new Uri(redirectUri);
        }

        var callbackMessage = new LogoutCallbackMessage()
        {
            PostLogoutRedirectUri = postLogoutRedirectUri,
            ClientName = message.ClientName,
            FrontChannelLogout = frontChannelLogout,
            SessionId = session?.Id,
            State = state,
            UiLocales = message.UiLocales,
            AutomaticRedirectAfterSignOut = accountOptions.AutomaticRedirectAfterSignOut
        };

        var logoutCallbackId = await messageStore.WriteAsync<LogoutCallbackMessage>(new(callbackMessage), ct);

        var url = serverOptions.UserInteraction.LoggingOutUrl;

        if (logoutCallbackId is not null)
        {
            url = url.AddQueryString(serverOptions.UserInteraction.LogoutIdParameter, logoutCallbackId);
        }

        return new Uri(url, UriKind.RelativeOrAbsolute);
    }
}
