using Microsoft.Extensions.DependencyInjection;
using RoyalIdentity.Extensions;
using RoyalIdentity.Storage.InMemory.Extensions;

namespace RoyalIdentity.Server;

public static class HostServices
{
    public static void AddHostServices(this IServiceCollection services)
    {
        // Services for the authentication server
        services.AddRoyalIdentityRazor();

        // Storage Services
        services.AddInMemoryStorage();

        // RoyalIdentity Services
        services.AddOpenIdConnectProviderServices();
    }
}
