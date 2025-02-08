using Microsoft.AspNetCore.Authentication.Cookies;
using RoyalIdentity.Extensions;
using RoyalIdentity.Storage.InMemory.Extensions;

namespace Tests.Host;

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
