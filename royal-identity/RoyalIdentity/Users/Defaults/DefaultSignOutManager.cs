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
    private readonly ILogger logger;

    public DefaultSignOutManager(
        IHttpContextAccessor httpContextAccessor,
        IUserSessionStore userSessionStore,
        IEventDispatcher eventDispatcher,
        IClientStore clients,
        IBackChannelLogoutNotifier backChannelNotifier,
        IMessageStore messageStore,
        IOptions<AccountOptions> accountOptions,
        ILogger<DefaultSignOutManager> logger)
    {
        this.httpContextAccessor = httpContextAccessor;
        this.userSessionStore = userSessionStore;
        this.eventDispatcher = eventDispatcher;
        this.backChannelNotifier = backChannelNotifier;
        this.clients = clients;
        this.messageStore = messageStore;
        this.accountOptions = accountOptions.Value;
        this.logger = logger;
    }

    public Task<string?> CreateLogoutIdAsync(CancellationToken ct)
    {


        throw new NotImplementedException();
    }

    public async Task<Uri> SignOutAsync(string sessionId, string? postLogoutRedirectUri, string? state, CancellationToken ct)
    {
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

        if (backChannelLogout.Count is not 0)
        {
            foreach(var backChannelRequest in backChannelLogout)
            {
                await backChannelNotifier.SendAsync(backChannelRequest, ct);
            }
        }

        var message = new LogoutCallbackMessage()
        {
            PostLogoutRedirectUri = postLogoutRedirectUri,
            FrontChannelLogout = frontChannelLogout,
            SessionId = session?.Id,
            State = state,
            AutomaticRedirectAfterSignOut = accountOptions.AutomaticRedirectAfterSignOut
        };

        var loggoutCallbackId = await messageStore.WriteAsync<LogoutCallbackMessage>(new(message), ct);

        return new("/account/logout/processing".AddQueryString("LogoutId", loggoutCallbackId));
    }
}
