namespace RoyalIdentity.Utils.Caching;

public class CacheOptions<TKey, TValue, TService>
    where TKey : notnull
{
    public required TimeSpan Expiration { get; set; }

    public required Func<TKey, TService, CancellationToken, Task<TValue>> Factory { get; set; }
}