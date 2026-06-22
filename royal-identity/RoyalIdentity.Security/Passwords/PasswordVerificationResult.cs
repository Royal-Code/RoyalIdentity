namespace RoyalIdentity.Security.Passwords;

/// <summary>
/// Outcome of <see cref="PasswordHash.Verify"/>. Verification never throws; malformed or unrecognized stored
/// hashes yield <see cref="Failed"/>.
/// </summary>
public enum PasswordVerificationResult
{
    /// <summary>The password did not match, or the stored hash was malformed/unrecognized.</summary>
    Failed = 0,

    /// <summary>The password matched a stored hash already in the current preferred format.</summary>
    Success = 1,

    /// <summary>
    /// The password matched, but the stored hash should be re-created in the current format (e.g. it is in the
    /// legacy format). Consumers may treat this as success and, optionally, rehash on login.
    /// </summary>
    SuccessRehashNeeded = 2
}
