using Microsoft.Extensions.DependencyInjection;

namespace RoyalIdentity.Utils.Caching;

public static class CacheExtensions
{
    /// <summary>
    /// <para>
    ///     Adds a cache to the service collection.
    /// </para>
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="expiration">The expiration time for the cache.</param>
    /// <param name="factory">The factory method to create the value for the cache.</param>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <typeparam name="TValue">The type of the value.</typeparam>
    /// <typeparam name="TService">The type of the service used to create the value.</typeparam>
    public static void AddCache<TKey, TValue, TService>(
        this IServiceCollection services,
        TimeSpan expiration,
        Func<TKey, TService, CancellationToken, Task<TValue>> factory)
        where TKey : notnull
    {
        services.AddSingleton(new CacheOptions<TKey, TValue, TService>
        {
            Expiration = expiration,
            Factory = factory
        });

        services.AddSingleton<CacheDictionary<TKey, TValue, TService>>();
        services.AddSingleton<ICached<TKey, TValue>, DefaultCached<TKey, TValue, TService>>();
    }
}