using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Components.Authorization;
using RoyalIdentity.Extensions;
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

        // TODO: Requer configurar com "ServerOptions.Authentication"
        // authentication
        services.AddAuthentication()
            .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
            {
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
            });

        // authorization
        services.AddAuthorization();

        return services;
    }
}