using System.Security.Cryptography;

namespace RoyalIdentity.Security.Cryptography;

/// <summary>
/// Constant-time equality checks for sensitive material (secrets, PKCE verifiers, digests). Backed by
/// <see cref="CryptographicOperations.FixedTimeEquals(ReadOnlySpan{byte}, ReadOnlySpan{byte})"/>, which does
/// not short-circuit on the first differing byte.
/// </summary>
/// <remarks>
/// <see cref="CryptographicOperations.FixedTimeEquals(ReadOnlySpan{byte}, ReadOnlySpan{byte})"/> returns
/// <see langword="false"/> immediately when the lengths differ. That is acceptable for the intended call
/// sites, which compare digests/hashes of deterministic length, so the length is not secret.
/// </remarks>
public static class FixedTimeComparer
{
    /// <summary>Compares two byte spans in constant time.</summary>
    public static bool IsEqual(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right)
        => CryptographicOperations.FixedTimeEquals(left, right);

    /// <summary>Compares the UTF-8 bytes of two strings in constant time.</summary>
    public static bool IsEqualUtf8(string left, string right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);

        return CryptographicOperations.FixedTimeEquals(
            System.Text.Encoding.UTF8.GetBytes(left),
            System.Text.Encoding.UTF8.GetBytes(right));
    }

    /// <summary>Compares the bytes decoded from two standard-Base64 strings in constant time.</summary>
    /// <exception cref="FormatException">Either argument is not valid Base64.</exception>
    public static bool IsEqualBase64(string leftBase64, string rightBase64)
    {
        ArgumentNullException.ThrowIfNull(leftBase64);
        ArgumentNullException.ThrowIfNull(rightBase64);

        return CryptographicOperations.FixedTimeEquals(
            Convert.FromBase64String(leftBase64),
            Convert.FromBase64String(rightBase64));
    }
}
