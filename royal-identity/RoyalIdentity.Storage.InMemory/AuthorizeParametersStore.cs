using System.Collections.Concurrent;
using System.Collections.Specialized;
using RoyalIdentity.Contracts.Storage;
using RoyalIdentity.Utils;

namespace RoyalIdentity.Storage.InMemory;

public class AuthorizeParametersStore : IAuthorizeParametersStore
{
    private readonly ConcurrentDictionary<string, NameValueCollection> authorizeParameters;

    public AuthorizeParametersStore(ConcurrentDictionary<string, NameValueCollection> authorizeParameters)
    {
        this.authorizeParameters = authorizeParameters;
    }

    public Task<string> WriteAsync(NameValueCollection parameters, CancellationToken ct)
    {
        var id = CryptoRandom.CreateUniqueId(16);
        authorizeParameters.TryAdd(id, parameters);
        return Task.FromResult(id);
    }

    public Task<NameValueCollection?> ReadAsync(string id, CancellationToken ct)
    {
        authorizeParameters.TryGetValue(id, out var parameters);
        return Task.FromResult(parameters);
    }

    public Task DeleteAsync(string id, CancellationToken ct)
    {
        authorizeParameters.TryRemove(id, out _);
        return Task.CompletedTask;
    }
}