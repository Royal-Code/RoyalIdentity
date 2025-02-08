using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Options;
using RoyalIdentity.Extensions;
using RoyalIdentity.Options;
using RoyalIdentity.Razor.Services;

// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring RoyalIdentity Razor on a service collection.
/// </summary>
public static class RoyalIdentityRazorServiceCollectionExtensions
{
    public static IServiceCollection AddRoyalIdentityRazor(this IServiceCollection services)
    {
        // Services for the authentication server, related to the AspNetCore project and views.
        services.AddCascadingAuthenticationState();
        services.AddScoped<IdentityUserManager>();
        services.AddScoped<IdentityRedirectManager>();
        services.AddScoped<AuthenticationStateProvider, IdentityRevalidatingAuthenticationStateProvider>();

        // authentication
        services.AddAuthentication().AddCookie(ServerConstants.DefaultCookieAuthenticationScheme);
        services.AddOptions<CookieAuthenticationOptions>(ServerConstants.DefaultCookieAuthenticationScheme)
            .Configure<IOptions<ServerOptions>>((cookieOptions, serverOptions) =>
            {
                var authOptions = serverOptions.Value.Authentication;
                var interactionOptions = serverOptions.Value.UserInteraction;
                var cookie = cookieOptions.Cookie;

                cookie.Name = authOptions.CookieName;
                cookie.SameSite = authOptions.CookieSameSiteMode;
                cookieOptions.ExpireTimeSpan = authOptions.CookieLifetime;
                cookieOptions.SlidingExpiration = authOptions.CookieSlidingExpiration;

                cookieOptions.LoginPath = interactionOptions.LoginPath;
                cookieOptions.LogoutPath = interactionOptions.LogoutPath;
                cookieOptions.ReturnUrlParameter = interactionOptions.ReturnUrlParameter;

                cookieOptions.Events.OnValidatePrincipal = async context =>
                {
                    if (context.Principal is null)
                        return;

                    var isSessionActive = await context.HttpContext.ValidateUserSessionAsync(context.Principal);
                    if (!isSessionActive)
                    {
                        context.RejectPrincipal();
                    }
                };
            });

        // authorization
        services.AddAuthorization();

        return services;
    }
}