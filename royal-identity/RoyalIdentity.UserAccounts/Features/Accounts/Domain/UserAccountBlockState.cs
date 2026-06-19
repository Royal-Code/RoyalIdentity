namespace RoyalIdentity.UserAccounts.Features.Accounts.Domain;

/// <summary>
/// Administrative block state for a user account.
/// </summary>
public class UserAccountBlockState
{
#nullable disable
	/// <summary>
	/// Constructor for EF Core.
	/// </summary>
	protected UserAccountBlockState()
	{
	}
#nullable restore

	private UserAccountBlockState(bool isBlocked, string? reason, DateTimeOffset? blockedAt)
	{
		IsBlocked = isBlocked;
		BlockedReason = reason;
		BlockedAt = blockedAt;
	}

	/// <summary>
	/// Gets whether the account is administratively blocked.
	/// </summary>
	public bool IsBlocked { get; private set; }

	/// <summary>
	/// Gets why the account was administratively blocked.
	/// </summary>
	public string? BlockedReason { get; private set; }

	/// <summary>
	/// Gets when the account was administratively blocked.
	/// </summary>
	public DateTimeOffset? BlockedAt { get; private set; }

	/// <summary>
	/// Creates the unblocked state.
	/// </summary>
	public static UserAccountBlockState Unblocked()
	{
		return new UserAccountBlockState(false, null, null);
	}

	/// <summary>
	/// Creates the blocked state.
	/// </summary>
	/// <param name="reason">Optional block reason.</param>
	/// <param name="blockedAt">The block timestamp.</param>
	public static UserAccountBlockState Blocked(string? reason, DateTimeOffset blockedAt)
	{
		return new UserAccountBlockState(true, reason, blockedAt);
	}
}
