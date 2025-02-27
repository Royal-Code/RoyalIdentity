using Microsoft.IdentityModel.Tokens;
using RoyalIdentity.Contracts.Storage;
using RoyalIdentity.Models.Keys;

namespace RoyalIdentity.Utils.Caching;

/// <summary>
/// Cache to store the keys obtained from the database.
/// </summary>
public sealed class KeyCache
{
    public KeyCache(IStorageProvider storageProvider, string realmId)
    {
        SigningCredentials = new(storageProvider, realmId);
        ValidationKeys = new(storageProvider, realmId);
    }

    /// <summary>
    /// Cache of keys for signing. It should only contain active keys.
    /// </summary>
    public KeyCacheEntry<IReadOnlyList<SigningCredentials>> SigningCredentials { get; }

    /// <summary>
    /// Cache of keys for validation. Must contain all keys, active and expired.
    /// </summary>
    public KeyCacheEntry<ValidationKeysInfo> ValidationKeys { get; }
}
