using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
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

        services.AddAuthentication(options =>
            {
                options.DefaultScheme = IdentityConstants.ApplicationScheme;
                options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
            })
            .AddIdentityCookies();

        // Storage Services
        services.AddInMemoryStorage();

        // RoyalIdentity Services
        services.AddOpenIdConnectProviderServices();
    }
}
