using RoyalIdentity.Contracts.Storage;
using RoyalIdentity.Models.Keys;
using System.Collections.Concurrent;

namespace RoyalIdentity.Storage.InMemory;

public class KeyStore : IKeyStore
{
    private readonly ConcurrentDictionary<string, KeyParameters> KeyParameters;

    public KeyStore(ConcurrentDictionary<string, KeyParameters> KeyParameters)
    {
        this.KeyParameters = KeyParameters;
    }

    public Task AddKeyAsync(KeyParameters key, CancellationToken ct)
    {
        KeyParameters[key.KeyId] = key;
        return Task.CompletedTask;
    }

    public Task<KeyParameters> GetKeyAsync(string keyId, CancellationToken ct)
    {
        KeyParameters.TryGetValue(keyId, out var key);

        if (key is null)
            throw new ArgumentException($"The key with the Id ‘{keyId}’ was not found", nameof(keyId));

        return Task.FromResult(key);
    }

    public async Task<IReadOnlyList<KeyParameters>> GetKeysAsync(IReadOnlyList<string> keyIds, CancellationToken ct)
    {
        List<KeyParameters> keyParameters = [];

        foreach(var keyId in keyIds)
        {
            var key = await GetKeyAsync(keyId, ct);

            keyParameters.Add(key);
        }

        return keyParameters;
    }

    public Task<IReadOnlyList<string>> ListAllCurrentKeysIdsAsync(DateTime? now = null, CancellationToken ct = default)
    {
        DateTime date = now ?? DateTime.UtcNow;

        IReadOnlyList<string> keyNames = KeyParameters.Values
            .Where(k => k.NotBefore == null || k.NotBefore <= date)
            .Where(k => k.Expires == null || k.Expires >= date)
            .OrderBy(k => k.Created)
            .Select(k => k.KeyId)
            .ToList();

        return Task.FromResult(keyNames);
    }

    public Task<IReadOnlyList<string>> ListAllKeysIdsAsync(DateTime? now = null, CancellationToken ct = default)
    {
        DateTime date = now ?? DateTime.UtcNow;

        IReadOnlyList<string> keyNames = KeyParameters.Values
            .Where(k => k.NotBefore == null || k.NotBefore <= date)
            .OrderBy(k => k.Created)
            .Select(k => k.KeyId)
            .ToList();

        return Task.FromResult(keyNames);
    }
}
