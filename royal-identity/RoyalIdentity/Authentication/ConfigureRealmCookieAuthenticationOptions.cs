using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Options;
using RoyalIdentity.Contracts.Storage;
using RoyalIdentity.Extensions;
using RoyalIdentity.Models;
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

        var cookie = options.Cookie;

        string? realmPath = null;
        Realm? realm = null;
        var authOptions = storage.ServerOptions.Authentication;

        if (name == Server.DefaultCookieAuthenticationScheme)
        {
            realmPath = Server.Realms.ServerRealm;
        }
        else if (name.StartsWith(Server.RealmAuthenticationNamePrefix))
        {
            realmPath = name[Server.RealmAuthenticationNamePrefix.Length..];
        }

        if (realmPath.IsMissing())
            return;

        realm = storage.Realms.GetByPath(realmPath);

        if (realm is not null && name != Server.DefaultCookieAuthenticationScheme)
            authOptions = realm.Options.Authentication;

        var cookieName = name == Server.DefaultCookieAuthenticationScheme
            ? authOptions.CookieName
            : $"{authOptions.CookieName}.{realmPath}";

        cookie.Name = cookieName;
        cookie.SameSite = authOptions.CookieSameSiteMode;
        options.ExpireTimeSpan = authOptions.CookieLifetime;
        options.SlidingExpiration = authOptions.CookieSlidingExpiration;

        if (realm is not null)
        {
            options.LoginPath = realm.Routes.LoginPath;
            options.LogoutPath = realm.Routes.LogoutPath;
            options.AccessDeniedPath = realm.Routes.AccessDeniedPath;
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
