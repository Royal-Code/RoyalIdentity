using RoyalIdentity.Contracts.Storage;
using RoyalIdentity.Models.Tokens;
using System.Collections.Concurrent;

namespace RoyalIdentity.Storage.InMemory;

public class RefreshTokenStore : IRefreshTokenStore
{
    private readonly ConcurrentDictionary<string, RefreshToken> refreshTokens;

    public RefreshTokenStore(ConcurrentDictionary<string, RefreshToken> refreshTokens)
    {
        this.refreshTokens = refreshTokens;
    }

    public Task<RefreshToken?> GetAsync(string token, CancellationToken ct)
    {
        refreshTokens.TryGetValue(token, out var refreshToken);
        return Task.FromResult(refreshToken);
    }

    public Task RemoveAsync(string token, CancellationToken ct)
    {
        refreshTokens.TryRemove(token, out _);
        return Task.CompletedTask;
    }

    public Task StoreAsync(RefreshToken token, CancellationToken ct)
    {
        refreshTokens.TryAdd(token.Token, token);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(RefreshToken token, CancellationToken ct)
    {
        refreshTokens.TryUpdate(token.Token, token, token);
        return Task.CompletedTask;
    }
}
