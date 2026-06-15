namespace RoyalIdentity.Users;

/// <summary>
/// Reason a local authentication attempt failed. The borda maps every reason to a single generic
/// user-facing message (anti-enumeration); the specific reason is kept for events/audit only.
/// </summary>
public enum AuthenticationFailureReason
{
    /// <summary>No account matched the provided login identifier.</summary>
    NotFound,

    /// <summary>The account exists but is not active.</summary>
    Inactive,

    /// <summary>The credential (password) did not match.</summary>
    InvalidCredentials,

    /// <summary>The account is temporarily locked out by the lockout policy.</summary>
    Blocked
}

/// <summary>
/// Single result of a local authentication attempt — unifies the legacy
/// <c>CredentialsValidationResult</c> + <c>ValidateCredentialsResult</c> (both removed). On success it
/// carries the <see cref="Subject"/> the borda will use protocolarmente; on failure it carries the
/// <see cref="AuthenticationFailureReason"/>.
/// </summary>
public sealed record AuthenticationResult
{
    private AuthenticationResult(bool success, Subject? subject, AuthenticationFailureReason? reason)
    {
        Success = success;
        Subject = subject;
        Reason = reason;
    }

    /// <summary>Whether authentication succeeded.</summary>
    public bool Success { get; }

    /// <summary>The authenticated subject when <see cref="Success"/> is <c>true</c>; otherwise <c>null</c>.</summary>
    public Subject? Subject { get; }

    /// <summary>The failure reason when <see cref="Success"/> is <c>false</c>; otherwise <c>null</c>.</summary>
    public AuthenticationFailureReason? Reason { get; }

    /// <summary>Creates a successful result for the given subject.</summary>
    public static AuthenticationResult Succeeded(Subject subject) => new(true, subject, null);

    /// <summary>Creates a failed result with the given reason.</summary>
    public static AuthenticationResult Failed(AuthenticationFailureReason reason) => new(false, null, reason);
}
