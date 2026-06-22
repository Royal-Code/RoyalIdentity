using System.Security.Cryptography;

namespace RoyalIdentity.Security.Passwords;

/// <summary>
/// Parameters for creating PBKDF2 password hashes and for deciding whether a stored hash needs rehashing.
/// The defaults define the current project baseline (PBKDF2-HMAC-SHA256, 100,000 iterations, 16-byte salt,
/// 32-byte output), so current-format hashes are not flagged for rehash solely due to a parameter change.
/// </summary>
public sealed class PasswordHashOptions
{
    /// <summary>Shared default options instance.</summary>
    public static PasswordHashOptions Default { get; } = new();

    /// <summary>PBKDF2 iteration count. Higher is stronger and slower.</summary>
    public int Iterations { get; init; } = 100_000;

    /// <summary>Salt size in bytes.</summary>
    public int SaltSize { get; init; } = 16;

    /// <summary>Derived key (hash) size in bytes.</summary>
    public int HashSize { get; init; } = 32;

    /// <summary>PBKDF2 PRF hash algorithm. Supported: SHA-256, SHA-384, SHA-512.</summary>
    public HashAlgorithmName Algorithm { get; init; } = HashAlgorithmName.SHA256;
}
