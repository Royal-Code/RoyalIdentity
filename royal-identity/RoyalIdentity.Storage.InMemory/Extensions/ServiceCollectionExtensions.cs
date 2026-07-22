using Microsoft.Extensions.DependencyInjection;
using RoyalIdentity.Configuration;
using RoyalIdentity.Contracts.Storage;
using RoyalIdentity.Users.Contracts;

namespace RoyalIdentity.Storage.InMemory.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInMemoryStorage(this IServiceCollection services)
    {
        services.AddSingleton<MemoryStorage>();
        services.AddTransient<IStorage>(sp => sp.GetRequiredService<MemoryStorage>());
        services.AddSingleton<IStorageProvider, StorageProvider>();

        // Configuration snapshot source + mandatory refresh interval for the default host (plan DF7). The
        // core registers the holder and the hosted refresher; here we supply this backing's async source.
        services.AddScoped<IConfigurationSnapshotSource, InMemoryConfigurationSnapshotSource>();
        services.AddSingleton(new ConfigurationSnapshotRefreshOptions { RefreshInterval = TimeSpan.FromMinutes(5) });

        // Account ports gateway (Q1) backed by the in-memory store; swapped for the UserAccounts module later.
        services.AddTransient<IUserDirectory, MemoryUserDirectory>();

        return services;
    }
}
