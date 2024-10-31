using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace RoyalIdentity.Utils.Caching;

public class CacheDictionary<TKey, TValue, TService>
    where TKey : notnull
{
    private readonly ConcurrentDictionary<TKey, CachedValue<TValue>> cache = new();

    private readonly CacheOptions<TKey, TValue, TService> options;
    private readonly ILogger logger;

    public CacheDictionary(
        CacheOptions<TKey, TValue, TService> options,
        ILogger<CacheDictionary<TKey, TValue, TService>> logger)
    {
        this.options = options;
        this.logger = logger;
    }

    public async ValueTask<TValue> GetOrCreateAsync(TKey key, TService serviceFactory, CancellationToken ct)
    {
        if (cache.TryGetValue(key, out var cachedValue))
        {
            if (!cachedValue.IsExpired)
                return cachedValue.Value;

            await TryUpdateAsync(key, cachedValue, serviceFactory, ct);
            return cachedValue.Value;
        }

        cachedValue = await CreateAsync(key, serviceFactory, ct);
        return cachedValue.Value;
    }

    private async ValueTask<CachedValue<TValue>> CreateAsync(TKey key, TService serviceFactory, CancellationToken ct)
    {
        var value = await options.Factory(key, serviceFactory, ct);
        var expiration = DateTimeOffset.UtcNow.Add(options.Expiration);
        var cachedValue = new CachedValue<TValue>(value, expiration);
        cache.TryAdd(key, cachedValue);
        return cachedValue;
    }

    private async ValueTask TryUpdateAsync(TKey key, CachedValue<TValue> cachedValue, TService serviceFactory, CancellationToken ct)
    {
        try
        {
            var value = await options.Factory(key, serviceFactory, ct);
            var expiration = DateTimeOffset.UtcNow.Add(options.Expiration);
            cachedValue.Update(value, expiration);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to update cache");
        }
    }
}