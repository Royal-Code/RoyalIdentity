using RoyalIdentity.Models.Keys;
using System.Security.Cryptography.Xml;

namespace RoyalIdentity.Contracts.Storage;

public interface IKeyStore
{
    public Task AddKeyAsync(KeyParameters key, CancellationToken ct);

    /// <summary>
    /// <para>
    ///     Gets all the secret names of the current keys, which are fit for use on the specified day (today).
    /// </para>
    /// </summary>
    /// <param name="today">
    ///     Today's date, optional, to filter <see cref=‘KeyInfo.NotBefore’/> and <see cref=‘KeyInfo.Expires’/>.
    /// </param>
    /// <returns>
    ///     A list with the names of the key vault secrets for the keys currently valid for use.
    /// </returns>
    public Task<IReadOnlyList<string>> ListAllCurrentKeysIdsAsync(DateTime? now = null, CancellationToken ct = default);

    /// <summary>
    /// <para>
    ///     It obtains all the secret names of current and expired keys, it just doesn't include future keys.
    /// </para>
    /// </summary>
    /// <param name="today">
    ///     Today's date, optional, to filter <see cref=‘KeyInfo.NotBefore’/>.
    /// </param>
    /// <returns>
    ///     A list with the names of the key vault secrets for the keys valid for signature validation.
    /// </returns>
    public Task<IReadOnlyList<string>> ListAllKeysIdsAsync(DateTime? now = null, CancellationToken ct = default);

    public Task<KeyParameters> GetKeyAsync(string keyId, CancellationToken ct);

    public Task<IReadOnlyList<KeyParameters>> GetKeysAsync(IReadOnlyList<string> keyIds, CancellationToken ct);
}