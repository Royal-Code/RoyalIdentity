namespace RoyalIdentity.UserAccounts.Features.Accounts.Domain;

/// <summary>
/// Result of local account authentication within the UserAccounts domain.
/// </summary>
public sealed record LocalAuthenticationResult
{
	private LocalAuthenticationResult(
		bool success,
		string? subjectId,
		string? displayName,
		LocalAuthenticationFailureReason? reason,
		DateTimeOffset? lockoutEndAt)
	{
		Success = success;
		SubjectId = subjectId;
		DisplayName = displayName;
		Reason = reason;
		LockoutEndAt = lockoutEndAt;
	}

	/// <summary>
	/// Gets whether authentication succeeded.
	/// </summary>
	public bool Success { get; }

	/// <summary>
	/// Gets the immutable subject identifier when authentication succeeded.
	/// </summary>
	public string? SubjectId { get; }

	/// <summary>
	/// Gets the account display name when authentication succeeded. The integration edge needs it to build the
	/// protocol subject (OIDC <c>name</c>) without a second lookup.
	/// </summary>
	public string? DisplayName { get; }

	/// <summary>
	/// Gets the failure reason when authentication failed.
	/// </summary>
	public LocalAuthenticationFailureReason? Reason { get; }

	/// <summary>
	/// Gets the lockout expiration when the credential is temporarily locked.
	/// </summary>
	public DateTimeOffset? LockoutEndAt { get; }

	/// <summary>
	/// Creates a successful authentication result.
	/// </summary>
	/// <param name="account">The authenticated account.</param>
	/// <returns>A successful result.</returns>
	public static LocalAuthenticationResult Succeeded(UserAccount account)
	{
		return new(true, account.SubjectId, account.DisplayName, null, null);
	}

	/// <summary>
	/// Creates a failed authentication result.
	/// </summary>
	/// <param name="reason">The reason authentication failed.</param>
	/// <param name="lockoutEndAt">Optional lockout expiration.</param>
	/// <returns>A failed result.</returns>
	public static LocalAuthenticationResult Failed(
		LocalAuthenticationFailureReason reason,
		DateTimeOffset? lockoutEndAt = null)
	{
		return new(false, null, null, reason, lockoutEndAt);
	}
}
