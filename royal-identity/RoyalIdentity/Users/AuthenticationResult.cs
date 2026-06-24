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
/// <see cref="AuthenticationFailureReason"/>. A third state (ADR-017 §2.3): the credential is valid but a
/// <see cref="RequiredAction"/> gates completion — the subject is carried, but the borda issues no
/// session/cookie/token until the action is satisfied.
/// </summary>
public sealed record AuthenticationResult
{
    private AuthenticationResult(
        bool success, Subject? subject, AuthenticationFailureReason? reason, RequiredAction? requiredAction)
    {
        Success = success;
        Subject = subject;
        Reason = reason;
        RequiredAction = requiredAction;
    }

    /// <summary>Whether authentication succeeded (a session/token may be issued).</summary>
    public bool Success { get; }

    /// <summary>
    /// The subject when authentication produced one — on success, or when a <see cref="RequiredAction"/> gates
    /// completion (the credential was valid); otherwise <c>null</c>.
    /// </summary>
    public Subject? Subject { get; }

    /// <summary>The failure reason when the attempt failed; otherwise <c>null</c>.</summary>
    public AuthenticationFailureReason? Reason { get; }

    /// <summary>
    /// The action that must be completed before authentication yields a session/token (ADR-017 §2.3). When set,
    /// the credential was valid but completion is gated; <see cref="Success"/> is <c>false</c> and
    /// <see cref="Reason"/> is <c>null</c>.
    /// </summary>
    public RequiredAction? RequiredAction { get; }

    /// <summary>Creates a successful result for the given subject.</summary>
    public static AuthenticationResult Succeeded(Subject subject) => new(true, subject, null, null);

    /// <summary>Creates a failed result with the given reason.</summary>
    public static AuthenticationResult Failed(AuthenticationFailureReason reason) => new(false, null, reason, null);

    /// <summary>
    /// Creates a result for a valid credential whose completion is gated by a required action: the subject is
    /// carried for routing/challenge, but no session/token is issued (ADR-017 §2.3).
    /// </summary>
    public static AuthenticationResult RequiresAction(Subject subject, RequiredAction requiredAction)
        => new(false, subject, null, requiredAction);
}
