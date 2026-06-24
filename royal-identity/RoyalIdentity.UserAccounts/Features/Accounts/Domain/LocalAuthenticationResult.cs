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
		DateTimeOffset? lockoutEndAt,
		LocalRequiredAction? requiredAction)
	{
		Success = success;
		SubjectId = subjectId;
		DisplayName = displayName;
		Reason = reason;
		LockoutEndAt = lockoutEndAt;
		RequiredAction = requiredAction;
	}

	/// <summary>
	/// Gets whether authentication succeeded (a session/token may be issued).
	/// </summary>
	public bool Success { get; }

	/// <summary>
	/// Gets the immutable subject identifier when authentication produced one (success, or a valid credential
	/// gated by a <see cref="RequiredAction"/>).
	/// </summary>
	public string? SubjectId { get; }

	/// <summary>
	/// Gets the account display name when authentication produced a subject. The integration edge needs it to
	/// build the protocol subject (OIDC <c>name</c>) without a second lookup.
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
	/// Gets the required action that gates completion when the credential is valid but the account must act
	/// first (ADR-017 §2.3). When set, <see cref="Success"/> is <c>false</c> and <see cref="Reason"/> is
	/// <c>null</c>, while <see cref="SubjectId"/>/<see cref="DisplayName"/> are populated.
	/// </summary>
	public LocalRequiredAction? RequiredAction { get; }

	/// <summary>
	/// Creates a successful authentication result.
	/// </summary>
	/// <param name="account">The authenticated account.</param>
	/// <returns>A successful result.</returns>
	public static LocalAuthenticationResult Succeeded(UserAccount account)
	{
		return new(true, account.SubjectId, account.DisplayName, null, null, null);
	}

	/// <summary>
	/// Creates a result for a valid credential whose completion is gated by a required action (ADR-017 §2.3):
	/// the subject is carried for the challenge, but the result is not a success (no session/token).
	/// </summary>
	/// <param name="account">The authenticated account.</param>
	/// <param name="requiredAction">The action the account must complete before continuing.</param>
	/// <returns>A gated result carrying the subject and the required action.</returns>
	public static LocalAuthenticationResult RequiresAction(UserAccount account, LocalRequiredAction requiredAction)
	{
		return new(false, account.SubjectId, account.DisplayName, null, null, requiredAction);
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
		return new(false, null, null, reason, lockoutEndAt, null);
	}
}
