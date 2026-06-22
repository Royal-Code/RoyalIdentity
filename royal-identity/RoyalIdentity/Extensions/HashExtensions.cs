// Ignore Spelling: Sha

using SecurityHashExtensions = RoyalIdentity.Security.Cryptography.HashExtensions;

namespace RoyalIdentity.Extensions;

/// <summary>
/// Delegate wrapper kept for backward compatibility until Phase 7.
/// All members delegate to <see cref="SecurityHashExtensions"/>.
/// </summary>
public static class HashExtensions
{
    public static string Sha256(this string? input) => SecurityHashExtensions.Sha256(input);

    public static byte[] Sha256(this byte[] input) => SecurityHashExtensions.Sha256(input);

    public static string Sha512(this string input) => SecurityHashExtensions.Sha512(input);
}
