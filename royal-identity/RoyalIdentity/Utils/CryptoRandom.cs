using System.Security.Cryptography;

namespace RoyalIdentity.Utils;

/// <summary>
/// A class that mimics the standard Random class in the .NET Framework - but uses a random number generator internally.
/// </summary>
public static class CryptoRandom
{
    private static readonly RandomNumberGenerator Rng = RandomNumberGenerator.Create();

    /// <summary>
    /// Output format for unique IDs
    /// </summary>
    public enum OutputFormat
    {
        /// <summary>
        /// URL-safe Base64
        /// </summary>
        Base64Url,
        /// <summary>
        /// Base64
        /// </summary>
        Base64,
        /// <summary>
        /// Hex
        /// </summary>
        Hex
    }

    /// <summary>
    /// Creates a random key byte array.
    /// </summary>
    /// <param name="length">The length.</param>
    /// <returns></returns>
    public static byte[] CreateRandomKey(int length)
    {
        var bytes = new byte[length];
        Rng.GetBytes(bytes);

        return bytes;
    }

    /// <summary>
    /// Creates a random key for the specified byte array.
    /// </summary>
    /// <returns></returns>
    public static void CreateRandomKey(byte[] bytes)
    {
        Rng.GetBytes(bytes);
    }

    /// <summary>
    /// Creates a URL safe unique identifier.
    /// </summary>
    /// <param name="length">The length.</param>
    /// <param name="format">The output format</param>
    /// <returns></returns>
    public static string CreateUniqueId(int length = 33, OutputFormat format = OutputFormat.Base64Url)
    {
        var bytes = CreateRandomKey(length);

        return format switch
        {
            OutputFormat.Base64Url => Base64Url.Encode(bytes),
            OutputFormat.Base64 => Convert.ToBase64String(bytes),
            OutputFormat.Hex => BitConverter.ToString(bytes).Replace("-", ""),
            _ => throw new ArgumentException("Invalid output format", nameof(format)),
        };
    }

    /// <summary>
    /// Returns a nonnegative random number.
    /// </summary>
    /// <returns>
    /// A 32-bit signed integer greater than or equal to zero and less than <see cref="F:System.Int32.MaxValue"/>.
    /// </returns>
    public static int Next()
    {
        var buffer = new byte[4];
        Rng.GetBytes(buffer);
        return BitConverter.ToInt32(buffer, 0) & 0x7FFFFFFF;
    }

    /// <summary>
    /// Returns a nonnegative random number less than the specified maximum.
    /// </summary>
    /// <param name="maxValue">The exclusive upper bound of the random number to be generated. <paramref name="maxValue"/> must be greater than or equal to zero.</param>
    /// <returns>
    /// A 32-bit signed integer greater than or equal to zero, and less than <paramref name="maxValue"/>; that is, the range of return values ordinarily includes zero but not <paramref name="maxValue"/>. However, if <paramref name="maxValue"/> equals zero, <paramref name="maxValue"/> is returned.
    /// </returns>
    /// <exception cref="T:System.ArgumentOutOfRangeException">
    /// 	<paramref name="maxValue"/> is less than zero.
    /// </exception>
    public static int Next(int maxValue)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maxValue);
        return Next(0, maxValue);
    }

    /// <summary>
    /// Returns a random number within a specified range.
    /// </summary>
    /// <param name="minValue">The inclusive lower bound of the random number returned.</param>
    /// <param name="maxValue">The exclusive upper bound of the random number returned. <paramref name="maxValue"/> must be greater than or equal to <paramref name="minValue"/>.</param>
    /// <returns>
    /// A 32-bit signed integer greater than or equal to <paramref name="minValue"/> and less than <paramref name="maxValue"/>; that is, the range of return values includes <paramref name="minValue"/> but not <paramref name="maxValue"/>. If <paramref name="minValue"/> equals <paramref name="maxValue"/>, <paramref name="minValue"/> is returned.
    /// </returns>
    /// <exception cref="T:System.ArgumentOutOfRangeException">
    /// 	<paramref name="minValue"/> is greater than <paramref name="maxValue"/>.
    /// </exception>
    public static int Next(int minValue, int maxValue)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan(minValue, maxValue);
        if (minValue == maxValue)
            return minValue;

        var diff = maxValue - minValue;
        var buffer = new byte[4];

        while (true)
        {
            Rng.GetBytes(buffer);
            var rand = BitConverter.ToUInt32(buffer, 0);

            var max = 1 + (long)uint.MaxValue;
            var remainder = max % diff;
            if (rand < max - remainder)
            {
                return (int)(minValue + (rand % diff));
            }
        }
    }

    /// <summary>
    /// Returns a random number between 0.0 and 1.0.
    /// </summary>
    /// <returns>
    /// A double-precision floating point number greater than or equal to 0.0, and less than 1.0.
    /// </returns>
    public static double NextDouble()
    {
        var buffer = new byte[4];
        Rng.GetBytes(buffer);
        var rand = BitConverter.ToUInt32(buffer, 0);
        return rand / (1.0 + uint.MaxValue);
    }

    /// <summary>
    /// Fills the elements of a specified array of bytes with random numbers.
    /// </summary>
    /// <param name="buffer">An array of bytes to contain random numbers.</param>
    /// <exception cref="T:System.ArgumentNullException">
    /// 	<paramref name="buffer"/> is null.
    /// </exception>
    public static void NextBytes(byte[] buffer)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        Rng.GetBytes(buffer);
    }
}
