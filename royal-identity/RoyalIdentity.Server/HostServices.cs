using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Components.Authorization;
using RoyalIdentity.Extensions;
using RoyalIdentity.Server.Services;
using RoyalIdentity.Storage.InMemory.Extensions;

namespace RoyalIdentity.Server;

public static class HostServices
{
    public static void AddHostServices(this IServiceCollection services)
    {
        // Services for the authentication server, related to the AspNetCore project and views.
        services.AddCascadingAuthenticationState();
        services.AddScoped<IdentityUserManager>();
        services.AddScoped<IdentityRedirectManager>();
        services.AddScoped<AuthenticationStateProvider, IdentityRevalidatingAuthenticationStateProvider>();

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
        services.AddAuthorization();

        // Storage Services
        services.AddInMemoryStorage();

        // RoyalIdentity Services
        services.AddOpenIdConnectProviderServices();
    }
}
