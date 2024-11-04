using Microsoft.IdentityModel.Tokens;
using RoyalIdentity.Models.Keys;

namespace RoyalIdentity.Utils.Caching;

/// <summary>
/// Cache to store the keys obtained from the database.
/// </summary>
public sealed class KeyCache
{
    /// <summary>
    /// Cache of keys for signing. It should only contain active keys.
    /// </summary>
    public KeyCacheEntry<SigningCredentials> SigningCredentials { get; } = new();

    /// <summary>
    /// Cache of keys for validation. Must contain all keys, active and expired.
    /// </summary>
    public KeyCacheEntry<SecurityKeyInfo> ValidationKeys { get; } = new();
}
