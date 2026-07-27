using Microsoft.Extensions.DependencyInjection;
using RoyalIdentity.Extensions;
using RoyalIdentity.Server.Configuration;
using RoyalIdentity.Storage.InMemory.Extensions;

namespace RoyalIdentity.Server;

public static class HostServices
{
    public static void AddHostServices(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);

        services.AddRoyalIdentityServerConfiguration(configuration, environment);

        // Services for the authentication server
        services.AddRoyalIdentityRazor();

        // Storage Services
        services.AddInMemoryStorage();

        // RoyalIdentity Services
        services.AddOpenIdConnectProviderServices();
    }
}
