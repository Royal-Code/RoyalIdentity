namespace RoyalIdentity.UserAccounts.Features.Accounts.Domain;

/// <summary>
/// A required action surfaced by local authentication when the password is valid but completion is gated
/// (ADR-017 §2.3). The integration edge mirrors it onto <c>AuthenticationResult.RequiredAction</c>; the distinct
/// values keep the reason (admin-forced vs expired) for the module's own events/diagnostics, even though both
/// collapse to a single edge "change password" action.
/// </summary>
public enum LocalRequiredAction
{
    /// <summary>The user must change the password because an administrator flagged it (MustChangePassword).</summary>
    ChangePasswordMustChange,

    /// <summary>The user must change the password because it expired under the realm policy.</summary>
    ChangePasswordExpired
}
