using System.Security.Cryptography;
using RoyalIdentity.Security.Encoding;

namespace RoyalIdentity.Security.Cryptography;

/// <summary>
/// Cryptographically strong random helpers for opaque identifiers (authorization codes, refresh tokens,
/// JWT ids, salts) and bounded random numbers. All members delegate to the thread-safe static
/// <see cref="RandomNumberGenerator"/> APIs, so there is no shared mutable state and the type is safe to call
/// concurrently from hot paths.
/// </summary>
public static class CryptoRandom
{
    /// <summary>Creates a random byte array of <paramref name="length"/> bytes of entropy.</summary>
    public static byte[] CreateRandomKey(int length)
    {
        var bytes = new byte[length];
        RandomNumberGenerator.Fill(bytes);

        return bytes;
    }

    /// <summary>Fills <paramref name="bytes"/> with cryptographically strong random bytes.</summary>
    public static void CreateRandomKey(Span<byte> bytes) => RandomNumberGenerator.Fill(bytes);

    /// <summary>Fills <paramref name="bytes"/> with cryptographically strong random bytes.</summary>
    public static void CreateRandomKey(byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        RandomNumberGenerator.Fill(bytes);
    }

    /// <summary>
    /// Creates an opaque unique identifier from <paramref name="length"/> bytes of entropy, rendered in the
    /// requested <paramref name="format"/>. Note <paramref name="length"/> is the number of entropy bytes, not
    /// the length of the resulting string.
    /// </summary>
    public static string CreateUniqueId(int length = 33, OutputFormat format = OutputFormat.Base64Url)
    {
        var bytes = CreateRandomKey(length);

        return format switch
        {
            OutputFormat.Base64Url => Base64Url.Encode(bytes),
            OutputFormat.Base64 => Convert.ToBase64String(bytes),
            OutputFormat.Hex => Convert.ToHexString(bytes),
            _ => throw new ArgumentException("Invalid output format", nameof(format)),
        };
    }

    /// <summary>Returns a non-negative random integer in the range <c>[0, int.MaxValue)</c>.</summary>
    public static int Next() => RandomNumberGenerator.GetInt32(int.MaxValue);

    /// <summary>Returns a non-negative random integer in the range <c>[0, maxValue)</c>.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxValue"/> is negative.</exception>
    public static int Next(int maxValue)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maxValue);

        return maxValue == 0 ? 0 : RandomNumberGenerator.GetInt32(maxValue);
    }

    /// <summary>
    /// Returns a random integer in the range <c>[minValue, maxValue)</c>, free of modulo bias
    /// (delegates to <see cref="RandomNumberGenerator.GetInt32(int, int)"/>).
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="minValue"/> is greater than <paramref name="maxValue"/>.</exception>
    public static int Next(int minValue, int maxValue)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan(minValue, maxValue);

        return minValue == maxValue ? minValue : RandomNumberGenerator.GetInt32(minValue, maxValue);
    }

    /// <summary>Returns a random double in the range <c>[0.0, 1.0)</c>.</summary>
    public static double NextDouble()
    {
        Span<byte> buffer = stackalloc byte[4];
        RandomNumberGenerator.Fill(buffer);
        var rand = BitConverter.ToUInt32(buffer);

        return rand / (1.0 + uint.MaxValue);
    }

    /// <summary>Fills <paramref name="buffer"/> with cryptographically strong random bytes.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="buffer"/> is <see langword="null"/>.</exception>
    public static void NextBytes(byte[] buffer)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        RandomNumberGenerator.Fill(buffer);
    }
}
