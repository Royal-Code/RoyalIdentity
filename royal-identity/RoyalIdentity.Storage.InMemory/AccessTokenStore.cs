using RoyalIdentity.Contracts.Storage;
using RoyalIdentity.Models.Tokens;
using System.Collections.Concurrent;

namespace RoyalIdentity.Storage.InMemory;

public class AccessTokenStore : IAccessTokenStore
{
    private readonly ConcurrentDictionary<string, AccessToken> accessTokens;

    public AccessTokenStore(ConcurrentDictionary<string, AccessToken> accessTokens)
    {
        this.accessTokens = accessTokens;
    }

    public Task<string> StoreAsync(AccessToken token, CancellationToken ct)
    {
        accessTokens.TryAdd(token.Id, token);
        return Task.FromResult(token.Id);
    }

    public Task<AccessToken?> GetAsync(string jti, CancellationToken ct)
    {
        accessTokens.TryGetValue(jti, out var token);
        return Task.FromResult(token);
    }

    public Task RemoveAsync(string jti, CancellationToken ct)
    {
        accessTokens.TryRemove(jti, out _);
        return Task.CompletedTask;
    }

    public Task RemoveReferenceTokensAsync(string subjectId, string clientId, CancellationToken ct)
    {
        accessTokens.Where(kvp => kvp.Value.AccessTokenType == AccessTokenType.Reference &&
                                  kvp.Value.SubjectId == subjectId &&
                                  kvp.Value.ClientId == clientId)
            .ToList()
            .ForEach(kvp => accessTokens.TryRemove(kvp.Key, out _));

        return Task.CompletedTask;
    }
}