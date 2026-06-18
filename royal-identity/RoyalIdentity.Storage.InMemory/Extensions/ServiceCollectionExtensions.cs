using Microsoft.Extensions.DependencyInjection;
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

        // Account ports gateway (Q1) backed by the in-memory store; swapped for the UserAccounts module later.
        services.AddTransient<IUserDirectory, MemoryUserDirectory>();

        return services;
    }
}
