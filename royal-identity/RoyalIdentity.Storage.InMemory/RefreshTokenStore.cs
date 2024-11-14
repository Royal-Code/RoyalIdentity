using RoyalIdentity.Contracts.Storage;
using RoyalIdentity.Models.Tokens;

namespace RoyalIdentity.Storage.InMemory;

public class RefreshTokenStore : IRefreshTokenStore
{
    private readonly MemoryStorage storage;

    public RefreshTokenStore(MemoryStorage storage)
    {
        this.storage = storage;
    }

    public Task<RefreshToken?> GetAsync(string token, CancellationToken ct)
    {
        storage.RefreshTokens.TryGetValue(token, out var refreshToken);
        return Task.FromResult(refreshToken);
    }

    public Task RemoveAsync(string token, CancellationToken ct)
    {
        storage.AccessTokens.TryRemove(token, out _);
        return Task.CompletedTask;
    }

    public Task StoreAsync(RefreshToken token, CancellationToken ct)
    {
        storage.RefreshTokens.TryAdd(token.Token, token);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(RefreshToken token, CancellationToken ct)
    {
        storage.RefreshTokens.TryUpdate(token.Token, token, token);
        return Task.CompletedTask;
    }
}
