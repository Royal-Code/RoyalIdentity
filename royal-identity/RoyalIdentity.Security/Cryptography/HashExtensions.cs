namespace RoyalIdentity.Security.Cryptography;

/// <summary>
/// String/byte hashing extensions kept for ergonomics and to reduce churn at call sites. They delegate to the
/// central <see cref="Hashing"/> helpers and preserve the legacy behaviour of returning an empty string for
/// missing (null/whitespace) input.
/// </summary>
public static class HashExtensions
{
    /// <summary>SHA-256 of the UTF-8 bytes of <paramref name="input"/> as standard Base64, or empty if missing.</summary>
    public static string Sha256(this string? input)
        => string.IsNullOrWhiteSpace(input) ? string.Empty : Hashing.Sha256Base64(input);

    /// <summary>SHA-256 digest of <paramref name="input"/>.</summary>
    public static byte[] Sha256(this byte[] input) => Hashing.Sha256(input);

    /// <summary>SHA-512 of the UTF-8 bytes of <paramref name="input"/> as standard Base64, or empty if missing.</summary>
    public static string Sha512(this string? input)
        => string.IsNullOrWhiteSpace(input) ? string.Empty : Hashing.Sha512Base64(input);
}
