namespace RoyalIdentity.Users;

/// <summary>
/// Why a login attempt could not proceed, as a stable code rather than a sentence (plan-localization DF11).
/// </summary>
/// <remarks>
/// The core decides <i>what</i> happened; only the presentation edge decides what the user reads, and in which
/// culture. Sending an English phrase across this boundary is what made the message untranslatable and pushed
/// three configurable strings into realm options.
/// </remarks>
public enum LoginFlowErrorCode
{
    /// <summary>
    /// The credentials did not authenticate anyone. Wrong password, unknown account, inactive account and
    /// blocked account all collapse into this one code on purpose (DF12): distinguishing them to the user is
    /// account enumeration. The precise <c>AuthenticationFailureReason</c> still reaches the internal event.
    /// </summary>
    InvalidCredentials,

    /// <summary>
    /// The request could not be attributed to a realm. This is a composition or routing fault, not a
    /// credential problem.
    /// </summary>
    NoRealmContext,

    /// <summary>
    /// The supplied return address is absolute and does not belong to any pending authorization request —
    /// the open-redirect guard.
    /// </summary>
    InvalidReturnUrl
}
