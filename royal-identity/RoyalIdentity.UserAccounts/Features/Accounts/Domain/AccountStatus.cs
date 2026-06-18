namespace RoyalIdentity.UserAccounts.Features.Accounts.Domain;

/// <summary>
/// Lifecycle status for a user account.
/// </summary>
public enum AccountStatus
{
	/// <summary>
	/// The account can authenticate and be used normally.
	/// </summary>
	Active,

	/// <summary>
	/// The account is disabled by lifecycle rules.
	/// </summary>
	Inactive,

	/// <summary>
	/// The account is blocked by an administrative or security action.
	/// </summary>
	Blocked
}
