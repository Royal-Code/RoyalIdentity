using System.Collections.Specialized;
using RoyalIdentity.Contracts.Storage;
using RoyalIdentity.Utils;

namespace RoyalIdentity.Storage.InMemory;

public class AuthorizeParametersStore : IAuthorizeParametersStore
{
    private readonly MemoryStorage storage;

    public AuthorizeParametersStore(MemoryStorage storage)
    {
        this.storage = storage;
    }

    public Task<string> WriteAsync(NameValueCollection parameters, CancellationToken ct)
    {
        var id = CryptoRandom.CreateUniqueId(16);
        storage.AuthorizeParameters.TryAdd(id, parameters);
        return Task.FromResult(id);
    }

    public Task<NameValueCollection?> ReadAsync(string id, CancellationToken ct)
    {
        storage.AuthorizeParameters.TryGetValue(id, out var parameters);
        return Task.FromResult(parameters);
    }

    public Task DeleteAsync(string id, CancellationToken ct)
    {
        storage.AuthorizeParameters.TryRemove(id, out _);
        return Task.CompletedTask;
    }
}