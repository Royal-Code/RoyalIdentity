using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using RoyalIdentity.Options;
using RoyalIdentity.Extensions;
using RoyalIdentity.Contracts;
using RoyalIdentity.Events;
using Microsoft.AspNetCore.Authentication;
using RoyalIdentity.Contracts.Storage;
using RoyalIdentity.Contracts.Models.Messages;
using RoyalIdentity.Contracts.Models;

namespace RoyalIdentity.Users.Defaults;

public class DefaultSignOutManager : ISignOutManager
{
    private readonly IHttpContextAccessor httpContextAccessor;
    private readonly IStorage storage;
    private readonly IEventDispatcher eventDispatcher;
    private readonly IBackChannelLogoutNotifier backChannelNotifier;
    private readonly IMessageStore messageStore;
    private readonly ILogger logger;

    public DefaultSignOutManager(
        IHttpContextAccessor httpContextAccessor,
        IStorage storage,
        IEventDispatcher eventDispatcher,
        IBackChannelLogoutNotifier backChannelNotifier,
        IMessageStore messageStore,
        ILogger<DefaultSignOutManager> logger)
    {
        this.httpContextAccessor = httpContextAccessor;
        this.storage = storage;
        this.eventDispatcher = eventDispatcher;
        this.backChannelNotifier = backChannelNotifier;
        this.messageStore = messageStore;
        this.logger = logger;
    }

    public async Task<string?> CreateLogoutIdAsync(CancellationToken ct)
    {
        var httpContext = httpContextAccessor.HttpContext;
        if (httpContext is null 
            || httpContext.User.Identity is null 
            || !httpContext.User.Identity.IsAuthenticated
            || !httpContext.TryGetCurrentRealm(out var realm))
        {
            return null;
        }

        var identity = httpContext.User.Identity;
        var sid = identity.GetSessionId();

        LogoutMessage message = new()
        {
            RealmId = realm.Id,
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

        var realm = await storage.Realms.GetByIdAsync(message.RealmId, ct)
            ?? throw new InvalidOperationException("Invalid realm id");

        var userSessionStore = storage.GetUserSessionStore(realm);
        var session = await userSessionStore.EndSessionAsync(sessionId, ct);
        
        if (session is null)
        {
            logger.LogWarning("User session not found, session id: {SessionId}", sessionId);
        }

        var httpContext = httpContextAccessor.HttpContext;
        if (httpContext is not null && httpContext.User.IsAuthenticated())
        {
            // delete local realm authentication cookie
            var authenticationScheme = httpContext.GetRealmAuthenticationScheme();
            await httpContext.SignOutAsync(authenticationScheme);

            // raise the logout event
            await eventDispatcher.DispatchAsync(
                new UserLogoutSuccessEvent(httpContext.User.GetSubjectId(), session?.Id));

            logger.LogDebug("User logout success");
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
                var clients = storage.GetClientStore(realm);
                var client = await clients.FindClientByIdAsync(clientId, ct);
                if (client is null || !client.Enabled)
                    continue;

                foreach(var uri in client.FrontChannelLogoutUri)
                {
                    iss ??= httpContext.GetServerIssuerUri(realm.Options);

                    var logoutUri = client.FrontChannelLogoutSessionRequired
                        ? uri.AddQueryString(JwtClaimTypes.Issuer, iss).AddQueryString(JwtClaimTypes.SessionId, session.Id)
                        : uri;
                    
                    frontChannelLogout.Add(logoutUri);
                }

                foreach (var uri in client.BackChannelLogoutUri)
                {
                    iss ??= httpContext.GetServerIssuerUri(realm.Options);

                    backChannelLogout.Add(new LogoutBackChannelRequest()
                    {
                        Realm = realm,
                        ClientId = clientId,
                        Issuer = iss,
                        Subject = session.User.UserName,
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
        // must have a post logout uri, no front channel logouts, and automatic redirect after sign out
        var canRedirectToPostLogoutUri = postLogoutRedirectUri is not null &&
            frontChannelLogout.Count == 0 &&
            realm.Options.Account.AutomaticRedirectAfterSignOut;

        if (canRedirectToPostLogoutUri)
        {
            var redirectUri = postLogoutRedirectUri!;

            if (state.IsPresent())
                redirectUri = redirectUri.AddQueryString(EndSessionRequest.State, state);

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
            AutomaticRedirectAfterSignOut = realm.Options.Account.AutomaticRedirectAfterSignOut,
            SignOutIframeUrl = Oidc.Routes.BuildEndSessionCallbackUrl(realm.Path)
        };

        var logoutCallbackId = await messageStore.WriteAsync<LogoutCallbackMessage>(new(callbackMessage), ct);

        var url = realm.Routes.LoggingOutPath;

        if (logoutCallbackId is not null)
        {
            url = url.AddQueryString(realm.Options.UI.LogoutParameter, logoutCallbackId);
        }

        return new Uri(url, UriKind.RelativeOrAbsolute);
    }
}
