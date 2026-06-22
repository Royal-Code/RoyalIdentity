using RoyalIdentity.Security.Keys;

namespace RoyalIdentity.Contracts.Storage;

public interface IKeyStore
{
    public Task AddKeyAsync(KeyParameters key, CancellationToken ct);

    /// <summary>
    /// <para>
    ///     Gets all the secret names of the current keys, which are fit for use on the specified day (today).
    /// </para>
    /// </summary>
    public Task<IReadOnlyList<string>> ListAllCurrentKeysIdsAsync(DateTime? now = null, CancellationToken ct = default);

    /// <summary>
    /// <para>
    ///     It obtains all the secret names of current and expired keys, it just doesn't include future keys.
    /// </para>
    /// </summary>
    public Task<IReadOnlyList<string>> ListAllKeysIdsAsync(DateTime? now = null, CancellationToken ct = default);

    public Task<KeyParameters> GetKeyAsync(string keyId, CancellationToken ct);

    public Task<IReadOnlyList<KeyParameters>> GetKeysAsync(IReadOnlyList<string> keyIds, CancellationToken ct);
}
