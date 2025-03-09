using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Options;
using RoyalIdentity.Extensions;
using RoyalIdentity.Options;

namespace RoyalIdentity.Authentication;

public class ConfigureRealmCookieAuthenticationOptions : IConfigureNamedOptions<CookieAuthenticationOptions>
{
    private readonly ServerOptions serverOptions;

    public ConfigureRealmCookieAuthenticationOptions(IOptions<ServerOptions> serverOptions)
    {
        this.serverOptions = serverOptions.Value;
    }

    public void Configure(string? name, CookieAuthenticationOptions options)
    {
        if (string.IsNullOrEmpty(name))
            return;

        var authOptions = serverOptions.Authentication;
        var interactionOptions = serverOptions.UserInteraction;
        var cookie = options.Cookie;

        string? cookieName = null;
        string? realmPath = null;

        if (name == Server.DefaultCookieAuthenticationScheme)
        {
            cookieName = authOptions.CookieName;
        }
        else if (name.StartsWith(Server.RealmAuthenticationNamePrefix))
        {
            realmPath = name[Server.RealmAuthenticationNamePrefix.Length..];
            cookieName = $"{authOptions.CookieName}.{realmPath}";
        }

        if (cookieName is null)
            return;

        cookie.Name = cookieName;
        cookie.SameSite = authOptions.CookieSameSiteMode;
        options.ExpireTimeSpan = authOptions.CookieLifetime;
        options.SlidingExpiration = authOptions.CookieSlidingExpiration;

        if (realmPath.IsMissing())
        {
            options.LoginPath = interactionOptions.LoginPath;
            options.LogoutPath = interactionOptions.LogoutPath;
            options.AccessDeniedPath = interactionOptions.AccessDeniedPath;
            options.ReturnUrlParameter = interactionOptions.ReturnUrlParameter;
        }
        else
        {
            options.LoginPath = $"/{realmPath}{interactionOptions.LoginPath}";
            options.LogoutPath = $"/{realmPath}{interactionOptions.LogoutPath}";
            options.AccessDeniedPath = $"/{realmPath}{interactionOptions.AccessDeniedPath}";
            options.ReturnUrlParameter = interactionOptions.ReturnUrlParameter;
        }

        options.Events.OnValidatePrincipal = async context =>
        {
            if (context.Principal is null)
                return;

            var isSessionActive = await context.HttpContext.ValidateUserSessionAsync(context.Principal);
            if (!isSessionActive)
            {
                context.RejectPrincipal();
            }
        };
    }

    public void Configure(CookieAuthenticationOptions options)
        => Configure(Microsoft.Extensions.Options.Options.DefaultName, options);
}
