using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Encodings.Web;

namespace RoyalIdentity.Authentication;

public class RealmAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public RealmAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder) : base(options, logger, encoder)
    { }

#pragma warning disable CS0618 // Type or member is obsolete
    public RealmAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger, 
        UrlEncoder encoder,

        ISystemClock clock) : base(options, logger, encoder, clock)

    { }
#pragma warning restore CS0618 // Type or member is obsolete

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        string authenticationScheme;

        // try get the realm from the route
        if (Request.RouteValues.TryGetValue(Server.RealmRouteKey, out var realm))
        {
            authenticationScheme = $"{Server.RealmAuthenticationNamePrefix}{realm}";
        }
        // try get realm from context items
        else if (Context.Items.TryGetValue(Server.RealmRouteKey, out var item) && item is string realmItem)
        {
            realm = realmItem;
            authenticationScheme = $"{Server.RealmAuthenticationNamePrefix}{realmItem}";
        }
        // else, use cookie authentication
        else
        {
            authenticationScheme = Server.DefaultCookieAuthenticationScheme;
        }

        var result = await Context.AuthenticateAsync(authenticationScheme);

        if (!result.Succeeded || result.Principal == null)
        {
            return AuthenticateResult.Fail($"No valid authentication for realm {realm}");
        }

        return AuthenticateResult.Success(new AuthenticationTicket(result.Principal, Scheme.Name));
    }
}
