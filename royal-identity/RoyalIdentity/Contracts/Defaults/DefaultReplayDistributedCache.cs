using Microsoft.Extensions.Caching.Distributed;
using RoyalIdentity.Contracts.Storage;

namespace RoyalIdentity.Contracts.Defaults;

/// <summary>
/// Default implementation of the replay cache using IDistributedCache
/// </summary>
public class DefaultReplayDistributedCache : IReplayCache
{
    private const string Prefix = nameof(DefaultReplayDistributedCache) + ":";

    private readonly IDistributedCache cache;

    /// <summary>
    /// ctor
    /// </summary>
    /// <param name="cache"></param>
    public DefaultReplayDistributedCache(IDistributedCache cache)
    {
        this.cache = cache;
    }

    /// <inheritdoc />
    public async Task AddAsync(string purpose, string handle, DateTimeOffset expiration)
    {
        var options = new DistributedCacheEntryOptions
        {
            AbsoluteExpiration = expiration
        };

        await cache.SetAsync(Prefix + purpose + handle, new byte[] { }, options);
    }

    /// <inheritdoc />
    public async Task<bool> ExistsAsync(string purpose, string handle)
    {
        return (await cache.GetAsync(Prefix + purpose + handle, default)) != null;
    }
}