using RoyalIdentity.Contracts.Storage;

namespace RoyalIdentity.Contracts.Defaults;

/// <summary>
/// Default implementation of the replay cache using IDistributedCache
/// </summary>
public class DefaultReplayNoCache : IReplayCache
{
    /// <inheritdoc />
    public Task AddAsync(string purpose, string handle, DateTimeOffset expiration) => Task.CompletedTask;

    /// <inheritdoc />
    public Task<bool> ExistsAsync(string purpose, string handle) => Task.FromResult(false);
}