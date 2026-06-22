using SecurityRandom = RoyalIdentity.Security.Cryptography.CryptoRandom;

namespace RoyalIdentity.Utils;

/// <summary>
/// Delegate wrapper kept for backward compatibility until Phase 7.
/// All members delegate to <see cref="SecurityRandom"/>.
/// </summary>
public static class CryptoRandom
{
    public enum OutputFormat
    {
        Base64Url,
        Base64,
        Hex,
    }

    public static byte[] CreateRandomKey(int length) => SecurityRandom.CreateRandomKey(length);

    public static void CreateRandomKey(byte[] bytes) => SecurityRandom.CreateRandomKey(bytes);

    public static string CreateUniqueId(int length = 33, OutputFormat format = OutputFormat.Base64Url)
        => SecurityRandom.CreateUniqueId(length, (SecurityRandom.OutputFormat)(int)format);

    public static int Next() => SecurityRandom.Next();

    public static int Next(int maxValue) => SecurityRandom.Next(maxValue);

    public static int Next(int minValue, int maxValue) => SecurityRandom.Next(minValue, maxValue);

    public static double NextDouble() => SecurityRandom.NextDouble();

    public static void NextBytes(byte[] buffer) => SecurityRandom.NextBytes(buffer);
}
