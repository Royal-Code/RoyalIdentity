using Microsoft.EntityFrameworkCore;
using RoyalIdentity.Security.Cryptography;
using RoyalIdentity.UserAccounts.Features.Accounts.Domain;
using RoyalIdentity.UserAccounts.Infrastructure.Data;

namespace RoyalIdentity.UserAccounts.Features.Accounts.Commons;

/// <summary>
/// Issues and consumes <see cref="UserAccountActionToken"/> rows (ADR-017 §2.4). The raw token is generated from
/// strong randomness and returned only once at issuance; only its hash is stored. Issuing a token revokes the
/// account's previous active tokens of the same purpose. Consumption is <em>idempotent</em>: the consumable check
/// and the mark-consumed are a single conditional update, so a token can win the race at most once. Token hashing
/// uses <see cref="RoyalIdentity.Security"/> primitives — high-entropy opaque handles, hashed with SHA-256.
/// </summary>
public sealed class UserAccountActionTokenService(UserAccountsDbContext db)
{
	private const int TokenEntropyBytes = 32;

	/// <summary>
	/// Issues a new token for an account, revoking the account's previous active tokens of the same purpose.
	/// </summary>
	/// <param name="account">The owner account.</param>
	/// <param name="purpose">What the token authorizes.</param>
	/// <param name="targetValue">The normalized email/phone the token is bound to, or <c>null</c>.</param>
	/// <param name="createdAt">When the token is issued.</param>
	/// <param name="expiresAt">When the token expires (mandatory TTL).</param>
	/// <param name="ct">A cancellation token.</param>
	/// <returns>The raw token to deliver to the user (never persisted).</returns>
	public async Task<string> IssueAsync(
		UserAccount account,
		ActionTokenPurpose purpose,
		string? targetValue,
		DateTimeOffset createdAt,
		DateTimeOffset expiresAt,
		CancellationToken ct = default)
	{
		var active = await db.UserAccountActionTokens
			.Where(t => t.RealmId == account.RealmId
				&& t.UserAccountId == account.Id
				&& t.Purpose == purpose
				&& t.ConsumedAt == null
				&& t.RevokedAt == null)
			.ToListAsync(ct);

		foreach (var token in active)
		{
			token.Revoke(ActionTokenRevocationReason.Superseded, createdAt);
		}

		var rawToken = CryptoRandom.CreateUniqueId(TokenEntropyBytes, OutputFormat.Base64Url);
		var tokenHash = Hashing.Sha256Base64Url(rawToken);

		var issued = UserAccountActionToken.Issue(account, purpose, tokenHash, targetValue, createdAt, expiresAt);
		db.UserAccountActionTokens.Add(issued);

		return rawToken;
	}

	/// <summary>
	/// Gets whether the account has an active token of the given purpose issued at or after the threshold. Used to
	/// throttle re-issuance per realm policy.
	/// </summary>
	/// <param name="realmId">The owning realm.</param>
	/// <param name="userAccountId">The owner account identifier.</param>
	/// <param name="purpose">The token purpose.</param>
	/// <param name="threshold">The earliest issuance instant that still counts as recent.</param>
	/// <param name="now">The evaluation timestamp (for the expiration check).</param>
	/// <param name="ct">A cancellation token.</param>
	/// <returns><c>true</c> when a recent active token exists.</returns>
	public Task<bool> HasActiveTokenSinceAsync(
		string realmId,
		long userAccountId,
		ActionTokenPurpose purpose,
		DateTimeOffset threshold,
		DateTimeOffset now,
		CancellationToken ct = default)
	{
		return db.UserAccountActionTokens.AnyAsync(
			t => t.RealmId == realmId
				&& t.UserAccountId == userAccountId
				&& t.Purpose == purpose
				&& t.ConsumedAt == null
				&& t.RevokedAt == null
				&& t.ExpiresAt > now
				&& t.CreatedAt >= threshold,
			ct);
	}

	/// <summary>
	/// Finds a currently consumable token by its raw value and purpose, without consuming it. Returns <c>null</c>
	/// for a missing, consumed, revoked or expired token — the caller maps both to the same generic outcome.
	/// </summary>
	/// <param name="realmId">The owning realm.</param>
	/// <param name="purpose">The token purpose.</param>
	/// <param name="rawToken">The raw token supplied by the caller.</param>
	/// <param name="now">The evaluation timestamp.</param>
	/// <param name="ct">A cancellation token.</param>
	/// <returns>The token candidate, or <c>null</c>.</returns>
	public Task<ActionTokenCandidate?> FindConsumableAsync(
		string realmId,
		ActionTokenPurpose purpose,
		string rawToken,
		DateTimeOffset now,
		CancellationToken ct = default)
	{
		var tokenHash = Hashing.Sha256Base64Url(rawToken);

		return db.UserAccountActionTokens
			.Where(t => t.RealmId == realmId
				&& t.Purpose == purpose
				&& t.TokenHash == tokenHash
				&& t.ConsumedAt == null
				&& t.RevokedAt == null
				&& t.ExpiresAt > now)
			.Select(t => new ActionTokenCandidate(t.Id, t.UserAccountId, t.TargetValue))
			.FirstOrDefaultAsync(ct);
	}

	/// <summary>
	/// Atomically marks a token consumed if it is still consumable. The conditional update is the single source of
	/// truth for single-use: concurrent attempts on the same token see at most one affected row.
	/// </summary>
	/// <param name="tokenId">The candidate token identifier (from <see cref="FindConsumableAsync"/>).</param>
	/// <param name="now">The consumption timestamp.</param>
	/// <param name="ct">A cancellation token.</param>
	/// <returns><c>true</c> when this call consumed the token; <c>false</c> when it was already consumed/expired/revoked.</returns>
	public async Task<bool> TryConsumeAsync(long tokenId, DateTimeOffset now, CancellationToken ct = default)
	{
		var affected = await db.UserAccountActionTokens
			.Where(t => t.Id == tokenId
				&& t.ConsumedAt == null
				&& t.RevokedAt == null
				&& t.ExpiresAt > now)
			.ExecuteUpdateAsync(setters => setters.SetProperty(t => t.ConsumedAt, now), ct);

		return affected == 1;
	}
}

/// <summary>
/// A token row resolved by <see cref="UserAccountActionTokenService.FindConsumableAsync"/>, carrying just enough to
/// load the account and complete the flow.
/// </summary>
/// <param name="TokenId">The token identifier.</param>
/// <param name="UserAccountId">The owner account identifier.</param>
/// <param name="TargetValue">The normalized email/phone the token is bound to, or <c>null</c>.</param>
public sealed record ActionTokenCandidate(long TokenId, long UserAccountId, string? TargetValue);
