using Microsoft.AspNetCore.Authentication.Cookies;
using RoyalIdentity.Extensions;
using RoyalIdentity.Storage.InMemory.Extensions;

namespace Tests.Host;

public static class HostServices
{
    public static void AddHostServices(this IServiceCollection services)
    {
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
        services.AddAuthorization();

        // Storage Services
        services.AddInMemoryStorage();

        // RoyalIdentity Services
        services.AddOpenIdConnectProviderServices();
    }
}
