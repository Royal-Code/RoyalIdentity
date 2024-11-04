using RoyalIdentity.Contracts.Storage;
using RoyalIdentity.Models.Keys;

namespace RoyalIdentity.Storage.InMemory;

public class KeyStore : IKeyStore
{
    private readonly MemoryStorage storage;

    public KeyStore(MemoryStorage storage)
    {
        this.storage = storage;
    }

    public Task AddKeyAsync(KeyParameters key, CancellationToken ct)
    {
        storage.KeyParameters[key.KeyId] = key;
        return Task.CompletedTask;
    }

    public Task<KeyParameters> GetKeyAsync(string keyId, CancellationToken ct)
    {
        storage.KeyParameters.TryGetValue(keyId, out var key);

        if (key is null)
            throw new ArgumentException($"The key with the Id ‘{keyId}’ was not found", nameof(keyId));

        return Task.FromResult(key);
    }

    public async Task<IReadOnlyList<KeyParameters>> GetKeysAsync(IReadOnlyList<string> keyNames, CancellationToken ct)
    {
        List<KeyParameters> keyParameters = [];

        foreach(var keyName in keyNames)
        {
            var key = await GetKeyAsync(keyName, ct);

            keyParameters.Add(key);
        }

        return keyParameters;
    }

    public Task<IReadOnlyList<string>> ListAllCurrentKeysIdsAsync(DateTime? today = null, CancellationToken ct = default)
    {
        DateTime date = today ?? DateTime.UtcNow.Date;

        IReadOnlyList<string> keyNames = storage.KeyParameters.Values
            .Where(k => k.NotBefore <= date)
            .Where(k => k.Expires >= date)
            .OrderBy(k => k.Created)
            .Select(k => k.KeyId)
            .ToList();

        return Task.FromResult(keyNames);
    }

    public Task<IReadOnlyList<string>> ListAllKeysIdsAsync(DateTime? today = null, CancellationToken ct = default)
    {
        DateTime date = today ?? DateTime.UtcNow.Date;

        IReadOnlyList<string> keyNames = storage.KeyParameters.Values
            .Where(k => k.NotBefore <= date)
            .OrderBy(k => k.Created)
            .Select(k => k.KeyId)
            .ToList();

        return Task.FromResult(keyNames);
    }
}
