using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using RoyalIdentity.Options;
using RoyalIdentity.Users.Contracts;
using RoyalIdentity.Extensions;
using RoyalIdentity.Contracts;
using RoyalIdentity.Events;
using Microsoft.AspNetCore.Authentication;
using RoyalIdentity.Contracts.Storage;

namespace RoyalIdentity.Users.Defaults;

public class DefaultSignOutManager : ISignOutManager
{
    private readonly IHttpContextAccessor httpContextAccessor;
    private readonly IUserSessionStore userSessionStore;
    private readonly IEventDispatcher eventDispatcher;
    private readonly IClientStore clients;
    private readonly AccountOptions accountOptions;
    private readonly ILogger logger;

    public Task<string?> CreateLogoutIdAsync(CancellationToken ct)
    {


        throw new NotImplementedException();
    }

    public async Task<Uri> SignOutAsync(string sessionId, string? postLogoutRedirectUri, string? state, CancellationToken ct)
    {
        var session = await userSessionStore.EndSessionAsync(sessionId, ct);
        
        var httpContext = httpContextAccessor.HttpContext;
        if (httpContext is not null && httpContext.User.IsAuthenticated())
        {
            // delete local authentication cookie
            await httpContext.SignOutAsync();

            // raise the logout event
            await eventDispatcher.DispatchAsync(
                new UserLogoutSuccessEvent(httpContext.User.GetSubjectId(), session?.Id));
        }

        HashSet<string> frontChannelLogout = [];
        HashSet<string> backChannelLogout = [];

        if (session is not null)
        {
            foreach(var clientId in session.Clients)
            {
                var client = await clients.FindClientByIdAsync(clientId, ct);
                if (client is null || !client.Enabled)
                    continue;

                if (client.FrontChannelLogoutSessionRequired)
                    frontChannelLogout.AddRange(client.FrontChannelLogoutUri);

                if (client.BackChannelLogoutSessionRequired)
                    backChannelLogout.AddRange(client.BackChannelLogoutUri);
            }
        }

        postLogoutRedirectUri ??= "/account/loggedout";

        // TODO: create LoggingOutMessage and store

        // TODO: redirect to /account/loggingout?loggingOutId=....&postLogoutRedirectUri=...&state=...

        throw new NotImplementedException();
    }
}
