namespace RoyalIdentity.Utils.Caching;

public class DefaultCached<TKey, TValue, TService> : ICached<TKey, TValue>
    where TKey : notnull
{
    private readonly CacheDictionary<TKey, TValue, TService> cache;
    private readonly TService service;

    public DefaultCached(CacheDictionary<TKey, TValue, TService> cache, TService service)
    {
        this.cache = cache;
        this.service = service;
    }

    public ValueTask<TValue> GetAsync(TKey key, CancellationToken ct)
    {
        return cache.GetOrCreateAsync(key, service, ct);
    }
}