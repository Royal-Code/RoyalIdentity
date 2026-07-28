using RoyalIdentity.Extensions;

namespace Tests.Host;

public static class HostServices
{
    public static void AddHostServices(this IServiceCollection services)
    {
        // Add services to the container.
        services.AddRazorComponents()
            .AddInteractiveServerComponents();

        // Services for the authentication server
        services.AddRoyalIdentityRazor();

        // RoyalIdentity Services
        services.AddOpenIdConnectProviderServices();
    }
}
