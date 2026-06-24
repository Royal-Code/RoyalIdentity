namespace RoyalIdentity.Users;

/// <summary>
/// Kind of action a user must complete before authentication can yield an SSO session, code or token
/// (ADR-017 §2.3). Modeled as a typed structure rather than a new <see cref="AuthenticationFailureReason"/> so
/// future verifications (e.g. email/phone/MFA) extend it without reshaping the failure taxonomy. Today the only
/// action is a password change.
/// </summary>
public enum RequiredActionType
{
    /// <summary>The user must set a new local password before authentication completes.</summary>
    ChangePassword
}

/// <summary>
/// An action that gates the completion of an otherwise valid authentication: the credential was verified, but
/// the borda issues no session/cookie/token until the action is satisfied (ADR-017 §2.3 — emenda à ADR-014 §5).
/// Carried by <see cref="AuthenticationResult"/>; the integration edge mirrors it from the module's local result.
/// </summary>
public sealed record RequiredAction(RequiredActionType Type)
{
    /// <summary>A required password change (admin-forced <c>MustChangePassword</c> or an expired password).</summary>
    public static RequiredAction ChangePassword { get; } = new(RequiredActionType.ChangePassword);
}
