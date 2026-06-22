namespace RoyalIdentity.Security.Cryptography;

/// <summary>
/// Output text format for opaque identifiers produced by <see cref="CryptoRandom.CreateUniqueId"/>.
/// </summary>
public enum OutputFormat
{
    /// <summary>URL-safe Base64 without padding.</summary>
    Base64Url,

    /// <summary>Standard Base64.</summary>
    Base64,

    /// <summary>Uppercase hexadecimal.</summary>
    Hex
}
