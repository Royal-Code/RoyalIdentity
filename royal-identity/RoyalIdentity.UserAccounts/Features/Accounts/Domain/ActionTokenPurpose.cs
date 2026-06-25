namespace RoyalIdentity.UserAccounts.Features.Accounts.Domain;

/// <summary>
/// The purpose a <see cref="UserAccountActionToken"/> authorizes (ADR-017 §2.4). One token model serves password
/// recovery, email/phone verification and the forced change of an expired password; the purpose narrows what the
/// token may be consumed for so a token issued for one flow cannot be replayed in another.
/// </summary>
public enum ActionTokenPurpose
{
	/// <summary>A password recovery token consumed by the reset flow.</summary>
	PasswordRecovery,

	/// <summary>An email verification token bound to the target email value.</summary>
	EmailVerification,

	/// <summary>A phone verification token bound to the target phone value.</summary>
	PhoneVerification,

	/// <summary>A transitional token that authorizes changing an expired/forced password before re-login.</summary>
	ChangeExpiredPassword
}
