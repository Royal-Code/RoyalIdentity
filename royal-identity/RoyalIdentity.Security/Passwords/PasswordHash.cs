using System.Globalization;
using System.Security.Cryptography;
using RoyalIdentity.Security.Cryptography;

namespace RoyalIdentity.Security.Passwords;

/// <summary>
/// Reusable PBKDF2 password hashing with a versioned, self-describing format and backward compatibility with
/// the legacy <c>$PBKDF2$.{salt}.{hash}</c> format. All methods are stateless and thread-safe.
/// </summary>
/// <remarks>
/// <para>
/// The current format is <c>$RIPWD$1$PBKDF2-{SHA}${iterations}${base64 salt}${base64 hash}</c>; it carries the
/// algorithm, iteration count, salt and hash so it can be verified without external configuration.
/// </para>
/// <para>
/// <see cref="Verify"/> never throws: a malformed or unrecognized stored hash yields
/// <see cref="PasswordVerificationResult.Failed"/> (this is an intentional behaviour change from the legacy
/// helper, which threw on malformed input). Password policy and lockout are not handled here; they remain a
/// concern of the account domain.
/// </para>
/// </remarks>
public static class PasswordHash
{
    private const string Scheme = "RIPWD";
    private const string CurrentVersion = "1";
    private const string Pbkdf2Prefix = "PBKDF2-";

    // Legacy format produced by RoyalIdentity/Utils/PasswordHash.cs (PBKDF2-HMAC-SHA256, 100k iterations,
    // 16-byte salt, 32-byte hash). Kept verifiable; such hashes are reported as SuccessRehashNeeded.
    private const string LegacyPrefix = "$PBKDF2$.";
    private const int LegacyIterations = 100_000;
    private static readonly HashAlgorithmName LegacyAlgorithm = HashAlgorithmName.SHA256;

    /// <summary>Creates a hash for <paramref name="password"/> using the default options.</summary>
    public static string Create(string password) => Create(password, PasswordHashOptions.Default);

    /// <summary>Creates a hash for <paramref name="password"/> using the supplied <paramref name="options"/>.</summary>
    /// <exception cref="ArgumentException"><paramref name="options"/> uses an unsupported algorithm.</exception>
    public static string Create(string password, PasswordHashOptions options)
    {
        ArgumentNullException.ThrowIfNull(password);
        ArgumentNullException.ThrowIfNull(options);

        var algorithmToken = AlgorithmToToken(options.Algorithm);
        var salt = CryptoRandom.CreateRandomKey(options.SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, options.Iterations, options.Algorithm, options.HashSize);

        return string.Join('$',
            string.Empty,
            Scheme,
            CurrentVersion,
            algorithmToken,
            options.Iterations.ToString(CultureInfo.InvariantCulture),
            Convert.ToBase64String(salt),
            Convert.ToBase64String(hash));
    }

    /// <summary>
    /// Verifies <paramref name="password"/> against <paramref name="storedHash"/>. Returns
    /// <see cref="PasswordVerificationResult.SuccessRehashNeeded"/> for a matching legacy hash,
    /// <see cref="PasswordVerificationResult.Success"/> for a matching current hash, and
    /// <see cref="PasswordVerificationResult.Failed"/> otherwise (including malformed input — never throws).
    /// </summary>
    public static PasswordVerificationResult Verify(string password, string storedHash)
    {
        if (password is null || string.IsNullOrEmpty(storedHash))
            return PasswordVerificationResult.Failed;

        return storedHash.StartsWith(LegacyPrefix, StringComparison.Ordinal)
            ? VerifyLegacy(password, storedHash)
            : VerifyCurrent(password, storedHash);
    }

    /// <summary>
    /// Returns <see langword="true"/> if <paramref name="storedHash"/> is not in the format described by
    /// <paramref name="options"/> (legacy format, weaker iteration count, different algorithm/sizes, or
    /// malformed) and should therefore be re-created. Rehash-on-login orchestration is the consumer's concern.
    /// </summary>
    public static bool NeedsRehash(string storedHash, PasswordHashOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (string.IsNullOrEmpty(storedHash))
            return true;

        if (storedHash.StartsWith(LegacyPrefix, StringComparison.Ordinal))
            return true;

        if (!TryParseCurrent(storedHash, out var parsed))
            return true;

        return parsed.Algorithm != options.Algorithm
            || parsed.Iterations < options.Iterations
            || parsed.Hash.Length != options.HashSize
            || parsed.Salt.Length != options.SaltSize;
    }

    private static PasswordVerificationResult VerifyLegacy(string password, string storedHash)
    {
        var parts = storedHash[LegacyPrefix.Length..].Split('.');
        if (parts.Length is not 2
            || !TryFromBase64(parts[0], out var salt)
            || !TryFromBase64(parts[1], out var expected)
            || expected.Length is 0)
        {
            return PasswordVerificationResult.Failed;
        }

        var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, LegacyIterations, LegacyAlgorithm, expected.Length);

        return CryptographicOperations.FixedTimeEquals(actual, expected)
            ? PasswordVerificationResult.SuccessRehashNeeded
            : PasswordVerificationResult.Failed;
    }

    private static PasswordVerificationResult VerifyCurrent(string password, string storedHash)
    {
        if (!TryParseCurrent(storedHash, out var parsed))
            return PasswordVerificationResult.Failed;

        var actual = Rfc2898DeriveBytes.Pbkdf2(password, parsed.Salt, parsed.Iterations, parsed.Algorithm, parsed.Hash.Length);

        return CryptographicOperations.FixedTimeEquals(actual, parsed.Hash)
            ? PasswordVerificationResult.Success
            : PasswordVerificationResult.Failed;
    }

    private static bool TryParseCurrent(string storedHash, out ParsedHash parsed)
    {
        parsed = default;

        // "$RIPWD$1$PBKDF2-SHA256$100000$<salt>$<hash>" -> ["", "RIPWD", "1", "PBKDF2-SHA256", "100000", salt, hash]
        var parts = storedHash.Split('$');
        if (parts.Length is not 7
            || parts[0].Length is not 0
            || !string.Equals(parts[1], Scheme, StringComparison.Ordinal)
            || !string.Equals(parts[2], CurrentVersion, StringComparison.Ordinal)
            || !TryTokenToAlgorithm(parts[3], out var algorithm)
            || !int.TryParse(parts[4], NumberStyles.None, CultureInfo.InvariantCulture, out var iterations)
            || iterations <= 0
            || !TryFromBase64(parts[5], out var salt)
            || !TryFromBase64(parts[6], out var hash)
            || hash.Length is 0)
        {
            return false;
        }

        parsed = new ParsedHash(algorithm, iterations, salt, hash);
        return true;
    }

    private static string AlgorithmToToken(HashAlgorithmName algorithm)
    {
        if (algorithm == HashAlgorithmName.SHA256)
            return Pbkdf2Prefix + "SHA256";
        if (algorithm == HashAlgorithmName.SHA384)
            return Pbkdf2Prefix + "SHA384";
        if (algorithm == HashAlgorithmName.SHA512)
            return Pbkdf2Prefix + "SHA512";

        throw new ArgumentException($"Unsupported password hashing algorithm: {algorithm.Name}", nameof(algorithm));
    }

    private static bool TryTokenToAlgorithm(string token, out HashAlgorithmName algorithm)
    {
        switch (token)
        {
            case Pbkdf2Prefix + "SHA256": algorithm = HashAlgorithmName.SHA256; return true;
            case Pbkdf2Prefix + "SHA384": algorithm = HashAlgorithmName.SHA384; return true;
            case Pbkdf2Prefix + "SHA512": algorithm = HashAlgorithmName.SHA512; return true;
            default: algorithm = default; return false;
        }
    }

    private static bool TryFromBase64(string value, out byte[] bytes)
    {
        var buffer = new byte[(value.Length / 4 + 1) * 3];
        if (Convert.TryFromBase64String(value, buffer, out var written))
        {
            bytes = buffer.AsSpan(0, written).ToArray();
            return true;
        }

        bytes = [];
        return false;
    }

    private readonly record struct ParsedHash(HashAlgorithmName Algorithm, int Iterations, byte[] Salt, byte[] Hash);
}
