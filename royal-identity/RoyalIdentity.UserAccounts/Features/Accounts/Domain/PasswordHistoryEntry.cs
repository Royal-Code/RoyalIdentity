using RoyalCode.Entities;

namespace RoyalIdentity.UserAccounts.Features.Accounts.Domain;

/// <summary>
/// An archived password hash kept so password reuse can be rejected by realm policy (ADR-017 §2.2).
/// Stores only a strong, self-contained hash; the plain password never reaches this entity.
/// </summary>
public class PasswordHistoryEntry : Entity<long>
{
#nullable disable
	/// <summary>
	/// Constructor for EF Core.
	/// </summary>
	protected PasswordHistoryEntry()
	{
	}
#nullable restore

	/// <summary>
	/// Creates an archived password history entry.
	/// </summary>
	/// <param name="realmId">The realm that owns the history row.</param>
	/// <param name="passwordHash">The archived (previous) password hash.</param>
	/// <param name="createdAt">When the password was archived.</param>
	/// <param name="reason">Why the password that displaced this hash was set.</param>
	/// <param name="createdBySubjectId">The actor who changed the password, or <c>null</c> for self-service/system.</param>
	public PasswordHistoryEntry(
		string realmId,
		string passwordHash,
		DateTimeOffset createdAt,
		PasswordChangeReason reason,
		string? createdBySubjectId)
	{
		RealmId = realmId;
		PasswordHash = passwordHash;
		CreatedAt = createdAt;
		Reason = reason;
		CreatedBySubjectId = createdBySubjectId;
	}

	/// <summary>
	/// Gets the realm that owns this history row.
	/// </summary>
	public string RealmId { get; private set; } = string.Empty;

	/// <summary>
	/// Gets the owner account foreign key.
	/// </summary>
	public long UserAccountId { get; private set; }

	/// <summary>
	/// Gets the owner account navigation.
	/// </summary>
	public virtual UserAccount? UserAccount { get; private set; }

	/// <summary>
	/// Gets the archived password hash.
	/// </summary>
	public string PasswordHash { get; private set; } = string.Empty;

	/// <summary>
	/// Gets when this password was archived.
	/// </summary>
	public DateTimeOffset CreatedAt { get; private set; }

	/// <summary>
	/// Gets why the password that displaced this hash was set.
	/// </summary>
	public PasswordChangeReason Reason { get; private set; }

	/// <summary>
	/// Gets the actor who changed the password, or <c>null</c> for self-service/system.
	/// </summary>
	public string? CreatedBySubjectId { get; private set; }

	/// <summary>
	/// Attaches this entry to its owning aggregate.
	/// </summary>
	/// <param name="account">The owner account.</param>
	internal void AttachTo(UserAccount account)
	{
		RealmId = account.RealmId;
		UserAccountId = account.Id;
		UserAccount = account;
	}
}
