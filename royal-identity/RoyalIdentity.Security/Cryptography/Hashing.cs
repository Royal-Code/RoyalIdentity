using System.Security.Cryptography;
using RoyalIdentity.Security.Encoding;

namespace RoyalIdentity.Security.Cryptography;

/// <summary>
/// Generic SHA hashing helpers. These are protocol-agnostic primitives; the OIDC meaning of the
/// left-half hash (<c>at_hash</c>, <c>c_hash</c>, <c>s_hash</c>) stays in the IdP core, which builds on
/// <see cref="LeftHalfHashBase64Url"/>.
/// </summary>
/// <remarks>
/// All methods use stateless <c>SHA*.HashData</c> APIs and are safe to call concurrently. String overloads
/// that render <c>Base64</c>/<c>Base64Url</c> use UTF-8 to match the byte semantics of the legacy
/// <c>HashExtensions</c>; <see cref="LeftHalfHashBase64Url"/> uses ASCII to match the legacy OIDC hash claims.
/// </remarks>
public static class Hashing
{
    /// <summary>Computes the SHA-256 digest of <paramref name="bytes"/>.</summary>
    public static byte[] Sha256(ReadOnlySpan<byte> bytes) => SHA256.HashData(bytes);

    /// <summary>Computes the SHA-384 digest of <paramref name="bytes"/>.</summary>
    public static byte[] Sha384(ReadOnlySpan<byte> bytes) => SHA384.HashData(bytes);

    /// <summary>Computes the SHA-512 digest of <paramref name="bytes"/>.</summary>
    public static byte[] Sha512(ReadOnlySpan<byte> bytes) => SHA512.HashData(bytes);

    /// <summary>SHA-256 of the UTF-8 bytes of <paramref name="value"/>, rendered as standard Base64.</summary>
    public static string Sha256Base64(string value)
        => Convert.ToBase64String(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(value)));

    /// <summary>SHA-512 of the UTF-8 bytes of <paramref name="value"/>, rendered as standard Base64.</summary>
    public static string Sha512Base64(string value)
        => Convert.ToBase64String(SHA512.HashData(System.Text.Encoding.UTF8.GetBytes(value)));

    /// <summary>SHA-256 of the UTF-8 bytes of <paramref name="value"/>, rendered as URL-safe Base64.</summary>
    public static string Sha256Base64Url(string value)
        => Base64Url.Encode(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(value)));

    /// <summary>
    /// Computes the left-most half of the hash of the ASCII octets of <paramref name="value"/> with the given
    /// <paramref name="algorithm"/>, rendered as URL-safe Base64. This is the building block for the OIDC
    /// <c>at_hash</c>/<c>c_hash</c>/<c>s_hash</c> claims (the protocol meaning stays in the core).
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="algorithm"/> is not SHA-256/384/512.</exception>
    public static string LeftHalfHashBase64Url(string value, HashAlgorithmName algorithm)
    {
        var hash = ComputeHash(System.Text.Encoding.ASCII.GetBytes(value), algorithm);
        var leftHalf = hash.AsSpan(0, hash.Length / 2);

        return Base64Url.Encode(leftHalf);
    }

    private static byte[] ComputeHash(ReadOnlySpan<byte> bytes, HashAlgorithmName algorithm)
    {
        if (algorithm == HashAlgorithmName.SHA256)
            return SHA256.HashData(bytes);
        if (algorithm == HashAlgorithmName.SHA384)
            return SHA384.HashData(bytes);
        if (algorithm == HashAlgorithmName.SHA512)
            return SHA512.HashData(bytes);

        throw new ArgumentException($"Unsupported hash algorithm: {algorithm.Name}", nameof(algorithm));
    }
}
