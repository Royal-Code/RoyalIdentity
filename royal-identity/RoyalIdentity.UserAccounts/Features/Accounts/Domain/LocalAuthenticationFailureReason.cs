namespace RoyalIdentity.UserAccounts.Features.Accounts.Domain;

/// <summary>
/// Reasons a local account authentication attempt can fail.
/// </summary>
public enum LocalAuthenticationFailureReason
{
	/// <summary>
	/// The supplied credential did not match.
	/// </summary>
	InvalidCredentials,

	/// <summary>
	/// The account has no local password credential.
	/// </summary>
	PasswordNotSet,

	/// <summary>
	/// The account is inactive.
	/// </summary>
	Inactive,

	/// <summary>
	/// The account is administratively blocked.
	/// </summary>
	Blocked,

}
