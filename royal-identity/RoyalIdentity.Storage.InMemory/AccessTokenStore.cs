using RoyalIdentity.Contracts.Storage;
using RoyalIdentity.Models.Tokens;

namespace RoyalIdentity.Storage.InMemory;

public class AccessTokenStore : IAccessTokenStore
{
    private readonly MemoryStorage storage;

    public AccessTokenStore(MemoryStorage storage)
    {
        this.storage = storage;
    }

    public Task<string> StoreAsync(AccessToken token, CancellationToken ct)
    {
        storage.AccessTokens.TryAdd(token.Id, token);
        return Task.FromResult(token.Id);
    }

    public Task<AccessToken?> GetAsync(string jti, CancellationToken ct)
    {
        storage.AccessTokens.TryGetValue(jti, out var token);
        return Task.FromResult(token);
    }

    public Task RemoveAsync(string jti, CancellationToken ct)
    {
        storage.AccessTokens.TryRemove(jti, out _);
        return Task.CompletedTask;
    }
}