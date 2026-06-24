using RoyalIdentity.UserAccounts.Options;

namespace RoyalIdentity.UserAccounts.Features.Accounts.Domain;

/// <summary>
/// Local password credential state for a user account.
/// </summary>
public class UserAccountCredential
{
#nullable disable
	/// <summary>
	/// Constructor for EF Core.
	/// </summary>
	protected UserAccountCredential()
	{
	}
#nullable restore

	/// <summary>
	/// Creates local credential state for an account.
	/// </summary>
	/// <param name="realmId">The realm that owns the credential row.</param>
	public UserAccountCredential(string realmId)
	{
		RealmId = realmId;
	}

	/// <summary>
	/// Gets the owner account foreign key and primary key for this 1:1 credential.
	/// </summary>
	public long UserAccountId { get; private set; }

	/// <summary>
	/// Gets the realm that owns this credential row.
	/// </summary>
	public string RealmId { get; private set; } = string.Empty;

	/// <summary>
	/// Gets the owner account navigation.
	/// </summary>
	public virtual UserAccount? UserAccount { get; private set; }

	/// <summary>
	/// Gets the stored password hash.
	/// </summary>
	public string? PasswordHash { get; private set; }

	/// <summary>
	/// Gets when the password was last changed.
	/// </summary>
	public DateTimeOffset? PasswordChangedAt { get; private set; }

	/// <summary>
	/// Gets whether the user must change the password on next supported flow.
	/// </summary>
	public bool MustChangePassword { get; private set; }

	/// <summary>
	/// Gets the number of consecutive failed password attempts.
	/// </summary>
	public int FailedPasswordAttempts { get; private set; }

	/// <summary>
	/// Gets when the last failed password attempt happened.
	/// </summary>
	public DateTimeOffset? LastPasswordFailureAt { get; private set; }

	/// <summary>
	/// Gets when temporary local credential lockout ends.
	/// </summary>
	public DateTimeOffset? LockoutEndAt { get; private set; }

	/// <summary>
	/// Gets whether a local password exists.
	/// </summary>
	public bool HasPassword => !string.IsNullOrWhiteSpace(PasswordHash);

	/// <summary>
	/// Attaches this credential to its owning aggregate.
	/// </summary>
	/// <param name="account">The owner account.</param>
	internal void AttachTo(UserAccount account)
	{
		RealmId = account.RealmId;
		UserAccountId = account.Id;
		UserAccount = account;
	}

	/// <summary>
	/// Stores a new password hash and resets failed-attempt state.
	/// </summary>
	/// <param name="passwordHash">The password hash to store.</param>
	/// <param name="changedAt">The change timestamp.</param>
	/// <param name="mustChangePassword">Whether the user must change the password later.</param>
	public void SetPassword(string passwordHash, DateTimeOffset changedAt, bool mustChangePassword)
	{
		PasswordHash = passwordHash;
		PasswordChangedAt = changedAt;
		MustChangePassword = mustChangePassword;
		ResetFailures();
	}

	/// <summary>
	/// Records a failed password attempt and applies lockout policy.
	/// </summary>
	/// <param name="options">Password and lockout policy.</param>
	/// <param name="now">The attempt timestamp.</param>
	/// <returns><c>true</c> when this attempt moved the credential into lockout.</returns>
	public bool RegisterFailure(PasswordOptions options, DateTimeOffset now)
	{
		FailedPasswordAttempts++;
		LastPasswordFailureAt = now;

		if (options.MaxFailedAccessAttempts <= 0 ||
			FailedPasswordAttempts < options.MaxFailedAccessAttempts)
		{
			return false;
		}

		LockoutEndAt = options.AccountLockoutDurationMinutes > 0
			? now.AddMinutes(options.AccountLockoutDurationMinutes)
			: null;

		return true;
	}

	/// <summary>
	/// Gets whether the credential is locked according to current policy.
	/// </summary>
	/// <param name="options">Password and lockout policy.</param>
	/// <param name="now">The timestamp used to evaluate lockout expiration.</param>
	/// <returns><c>true</c> when lockout is active.</returns>
	public bool IsLockedOut(PasswordOptions options, DateTimeOffset now)
	{
		if (options.MaxFailedAccessAttempts <= 0 ||
			FailedPasswordAttempts < options.MaxFailedAccessAttempts)
		{
			return false;
		}

		return LockoutEndAt is null || LockoutEndAt > now;
	}

	/// <summary>
	/// Gets whether the password has expired according to the realm expiration policy. Detection only — the
	/// caller routes an expired password to the change flow (it does not block authentication silently).
	/// </summary>
	/// <param name="options">Password and lockout policy.</param>
	/// <param name="now">The timestamp used to evaluate expiration.</param>
	/// <returns><c>true</c> when the password is expired.</returns>
	public bool IsPasswordExpired(PasswordOptions options, DateTimeOffset now)
	{
		if (!options.EnablePasswordExpiration ||
			options.PasswordExpirationDays <= 0 ||
			!HasPassword ||
			PasswordChangedAt is null)
		{
			return false;
		}

		return PasswordChangedAt.Value.AddDays(options.PasswordExpirationDays) <= now;
	}

	/// <summary>
	/// Clears an expired temporary lockout.
	/// </summary>
	/// <param name="options">Password and lockout policy.</param>
	/// <param name="now">The timestamp used to evaluate lockout expiration.</param>
	public void ClearExpiredLockout(PasswordOptions options, DateTimeOffset now)
	{
		if (options.MaxFailedAccessAttempts <= 0 ||
			FailedPasswordAttempts < options.MaxFailedAccessAttempts ||
			LockoutEndAt is null ||
			LockoutEndAt > now)
		{
			return;
		}

		ResetFailures();
	}

	/// <summary>
	/// Resets failed password attempts and lockout state.
	/// </summary>
	internal void ResetFailures()
	{
		FailedPasswordAttempts = 0;
		LastPasswordFailureAt = null;
		LockoutEndAt = null;
	}
}
