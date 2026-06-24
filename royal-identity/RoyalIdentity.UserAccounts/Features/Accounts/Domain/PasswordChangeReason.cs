namespace RoyalIdentity.UserAccounts.Features.Accounts.Domain;

/// <summary>
/// Why a password was set. Recorded on archived <see cref="PasswordHistoryEntry"/> rows for audit/history.
/// </summary>
public enum PasswordChangeReason
{
	/// <summary>The initial password set when the account was created.</summary>
	Create,

	/// <summary>A user-initiated password change.</summary>
	Change,

	/// <summary>A password reset performed via a recovery token.</summary>
	Reset,

	/// <summary>An administrative password set or reset.</summary>
	AdminSet,

	/// <summary>A password imported or migrated from another system.</summary>
	Import
}
