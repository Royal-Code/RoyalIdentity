using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Options;
using RoyalIdentity.Contracts.Storage;
using RoyalIdentity.Extensions;
using RoyalIdentity.Options;

namespace RoyalIdentity.Authentication;

public class ConfigureRealmCookieAuthenticationOptions : IConfigureNamedOptions<CookieAuthenticationOptions>
{
    private readonly IStorage storage;

    public ConfigureRealmCookieAuthenticationOptions(IStorage storage)
    {
        this.storage = storage;
    }

    public void Configure(string? name, CookieAuthenticationOptions options)
    {
        if (string.IsNullOrEmpty(name))
            return;

        var authOptions = storage.ServerOptions.Authentication;
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
            realmPath = Server.Realms.ServerRealm;

        var realm = storage.Realms.GetByPath(realmPath);

        if (realm is not null)
        {
            options.LoginPath = realm.Routes.LoginPath;
            options.LogoutPath = realm.Routes.LogoutPath;
            options.AccessDeniedPath = storage.ServerOptions.UI.AccessDeniedPath;
            options.ReturnUrlParameter = realm.Options.UI.LoginParameter;
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
