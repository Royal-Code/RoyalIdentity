namespace RoyalIdentity.Security.Keys;

/// <summary>
/// How the serialized <see cref="KeyParameters.Key"/> string is encoded at rest.
/// </summary>
public enum KeyEncoding
{
    /// <summary>The key string is stored as-is (no extra encoding layer).</summary>
    Plain,

    /// <summary>The key string is standard Base64.</summary>
    Base64,

    /// <summary>The key string is hexadecimal.</summary>
    Hex,
}
