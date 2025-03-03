using Microsoft.Extensions.DependencyInjection;
using RoyalIdentity.Contracts.Storage;

namespace RoyalIdentity.Storage.InMemory.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInMemoryStorage(this IServiceCollection services)
    {
        services.AddSingleton<MemoryStorage>();
        services.AddTransient<IStorage>(sp => sp.GetRequiredService<MemoryStorage>());
        services.AddSingleton<IStorageProvider, StorageProvider>();

        return services;
    }
}
