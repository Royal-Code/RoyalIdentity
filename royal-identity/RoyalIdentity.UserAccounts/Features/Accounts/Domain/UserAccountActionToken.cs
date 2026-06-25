using RoyalCode.Entities;

namespace RoyalIdentity.UserAccounts.Features.Accounts.Domain;

/// <summary>
/// A single-use action token issued for an account (ADR-017 §2.4): password recovery, email/phone verification or
/// a forced expired-password change. Only the token <em>hash</em> is stored — the raw token is shown once at
/// issuance and never reaches this entity, an event, an audit entry or a log. The token has a mandatory TTL, is
/// bound to an optional normalized <see cref="TargetValue"/> (the email/phone it verifies) and is consumed
/// idempotently through a conditional update (see <c>UserAccountActionTokenService</c>); a new issuance for the
/// same purpose revokes the previous active tokens.
/// </summary>
public class UserAccountActionToken : Entity<long>
{
#nullable disable
	/// <summary>
	/// Constructor for EF Core.
	/// </summary>
	protected UserAccountActionToken()
	{
	}
#nullable restore

	private UserAccountActionToken(
		string realmId,
		long userAccountId,
		ActionTokenPurpose purpose,
		string tokenHash,
		string? targetValue,
		DateTimeOffset createdAt,
		DateTimeOffset expiresAt)
	{
		RealmId = realmId;
		UserAccountId = userAccountId;
		Purpose = purpose;
		TokenHash = tokenHash;
		TargetValue = targetValue;
		CreatedAt = createdAt;
		ExpiresAt = expiresAt;
	}

	/// <summary>
	/// Issues a new action token for an account. The caller supplies the strong hash of an already generated raw
	/// token (kept out of this entity) and a non-null expiration.
	/// </summary>
	/// <param name="account">The owner account.</param>
	/// <param name="purpose">What the token authorizes.</param>
	/// <param name="tokenHash">The strong hash of the raw token.</param>
	/// <param name="targetValue">The normalized email/phone the token is bound to, or <c>null</c>.</param>
	/// <param name="createdAt">When the token was issued.</param>
	/// <param name="expiresAt">When the token expires (mandatory TTL).</param>
	/// <returns>The issued token, ready to be tracked by the unit of work.</returns>
	public static UserAccountActionToken Issue(
		UserAccount account,
		ActionTokenPurpose purpose,
		string tokenHash,
		string? targetValue,
		DateTimeOffset createdAt,
		DateTimeOffset expiresAt)
	{
		return new UserAccountActionToken(
			account.RealmId,
			account.Id,
			purpose,
			tokenHash,
			targetValue,
			createdAt,
			expiresAt);
	}

	/// <summary>
	/// Gets the realm that owns this token row.
	/// </summary>
	public string RealmId { get; private set; } = string.Empty;

	/// <summary>
	/// Gets the owner account foreign key.
	/// </summary>
	public long UserAccountId { get; private set; }

	/// <summary>
	/// Gets what the token authorizes.
	/// </summary>
	public ActionTokenPurpose Purpose { get; private set; }

	/// <summary>
	/// Gets the strong hash of the raw token. The raw token is never stored.
	/// </summary>
	public string TokenHash { get; private set; } = string.Empty;

	/// <summary>
	/// Gets the normalized email/phone value the token is bound to, or <c>null</c> when not value-bound.
	/// </summary>
	public string? TargetValue { get; private set; }

	/// <summary>
	/// Gets when the token was issued.
	/// </summary>
	public DateTimeOffset CreatedAt { get; private set; }

	/// <summary>
	/// Gets when the token expires.
	/// </summary>
	public DateTimeOffset ExpiresAt { get; private set; }

	/// <summary>
	/// Gets when the token was consumed, or <c>null</c> when it has not been consumed.
	/// </summary>
	public DateTimeOffset? ConsumedAt { get; private set; }

	/// <summary>
	/// Gets when the token was revoked, or <c>null</c> when it has not been revoked.
	/// </summary>
	public DateTimeOffset? RevokedAt { get; private set; }

	/// <summary>
	/// Gets why the token was revoked, or <c>null</c> when it has not been revoked.
	/// </summary>
	public ActionTokenRevocationReason? RevokedReason { get; private set; }

	/// <summary>
	/// Gets the hash of the issuer IP, populated by the edge. Never the raw IP.
	/// </summary>
	public string? CreatedIpHash { get; private set; }

	/// <summary>
	/// Gets the hash of the consumer IP, populated by the edge. Never the raw IP.
	/// </summary>
	public string? ConsumedIpHash { get; private set; }

	/// <summary>
	/// Gets the hash of the user agent, populated by the edge. Never the raw user agent.
	/// </summary>
	public string? UserAgentHash { get; private set; }

	/// <summary>
	/// Gets whether the token is still consumable at the given instant (not consumed, not revoked, not expired).
	/// </summary>
	/// <param name="now">The evaluation timestamp.</param>
	/// <returns><c>true</c> when the token can still be consumed.</returns>
	public bool IsConsumable(DateTimeOffset now)
		=> ConsumedAt is null && RevokedAt is null && ExpiresAt > now;

	/// <summary>
	/// Revokes the token if it is still active. A consumed or already-revoked token is left untouched.
	/// </summary>
	/// <param name="reason">Why the token is revoked.</param>
	/// <param name="now">The revocation timestamp.</param>
	public void Revoke(ActionTokenRevocationReason reason, DateTimeOffset now)
	{
		if (ConsumedAt is not null || RevokedAt is not null)
		{
			return;
		}

		RevokedAt = now;
		RevokedReason = reason;
	}
}
