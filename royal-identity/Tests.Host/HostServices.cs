using RoyalIdentity.Extensions;
using RoyalIdentity.Storage.InMemory.Extensions;

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

        // Storage Services
        services.AddInMemoryStorage();

        // RoyalIdentity Services
        services.AddOpenIdConnectProviderServices();
    }
}
