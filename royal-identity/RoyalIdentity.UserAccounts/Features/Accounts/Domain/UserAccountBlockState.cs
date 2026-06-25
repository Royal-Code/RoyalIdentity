namespace RoyalIdentity.UserAccounts.Features.Accounts.Domain;

/// <summary>
/// Administrative block state for a user account (ADR-017 §2.5). A block can be effective immediately and
/// indefinitely (the common case: <see cref="StartsAt"/> and <see cref="EndsAt"/> are <c>null</c>) or scheduled to a
/// time window (e.g. blocking an account while its owner is on vacation). This is a distinct state from the
/// credential lockout (failed-attempt throttling on <see cref="UserAccountCredential"/>): the two never conflate.
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

	private UserAccountBlockState(
		bool isBlocked,
		string? reason,
		DateTimeOffset? blockedAt,
		DateTimeOffset? startsAt,
		DateTimeOffset? endsAt)
	{
		IsBlocked = isBlocked;
		BlockedReason = reason;
		BlockedAt = blockedAt;
		StartsAt = startsAt;
		EndsAt = endsAt;
	}

	/// <summary>
	/// Gets whether an administrative block is configured. This is the raw configured flag — use
	/// <see cref="IsActiveAt"/> to evaluate whether the block is actually in effect at a point in time (a scheduled
	/// or expired window may be configured but not in effect).
	/// </summary>
	public bool IsBlocked { get; private set; }

	/// <summary>
	/// Gets why the account was administratively blocked.
	/// </summary>
	public string? BlockedReason { get; private set; }

	/// <summary>
	/// Gets when the administrator created the block (the action timestamp, for audit).
	/// </summary>
	public DateTimeOffset? BlockedAt { get; private set; }

	/// <summary>
	/// Gets when the block becomes effective. <c>null</c> means it is effective immediately.
	/// </summary>
	public DateTimeOffset? StartsAt { get; private set; }

	/// <summary>
	/// Gets when the block expires. <c>null</c> means the block is indefinite.
	/// </summary>
	public DateTimeOffset? EndsAt { get; private set; }

	/// <summary>
	/// Creates the unblocked state.
	/// </summary>
	public static UserAccountBlockState Unblocked()
	{
		return new UserAccountBlockState(false, null, null, null, null);
	}

	/// <summary>
	/// Creates the blocked state, optionally scheduled to a time window. Window validity (a closed window must be
	/// ordered, and a block must not already be expired) is checked by the feature that issues the block, not here.
	/// </summary>
	/// <param name="reason">Optional block reason.</param>
	/// <param name="blockedAt">The block action timestamp.</param>
	/// <param name="startsAt">When the block becomes effective, or <c>null</c> for immediately.</param>
	/// <param name="endsAt">When the block expires, or <c>null</c> for indefinite.</param>
	public static UserAccountBlockState Blocked(
		string? reason,
		DateTimeOffset blockedAt,
		DateTimeOffset? startsAt = null,
		DateTimeOffset? endsAt = null)
	{
		return new UserAccountBlockState(true, reason, blockedAt, startsAt, endsAt);
	}

	/// <summary>
	/// Gets whether the administrative block is in effect at <paramref name="now"/>: a block is configured and the
	/// instant falls within the optional [<see cref="StartsAt"/>, <see cref="EndsAt"/>) window.
	/// </summary>
	/// <param name="now">The instant to evaluate.</param>
	/// <returns><c>true</c> when the block is in effect.</returns>
	public bool IsActiveAt(DateTimeOffset now)
	{
		return IsBlocked
			&& (StartsAt is null || now >= StartsAt.Value)
			&& (EndsAt is null || now < EndsAt.Value);
	}
}
