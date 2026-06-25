using RoyalCode.Entities;

namespace RoyalIdentity.UserAccounts.Features.Accounts.Domain;

/// <summary>
/// Phone number owned by a user account (ADR-017 §2.8). Modeled like <see cref="UserAccountEmail"/> but more
/// optional (enabled per realm). There is no <c>VerifiedAt</c> (Q10): changing a number creates a new, unverified
/// object rather than resetting an existing one.
/// </summary>
public class UserAccountPhone : Entity<long>
{
#nullable disable
	/// <summary>
	/// Constructor for EF Core.
	/// </summary>
	protected UserAccountPhone()
	{
	}
#nullable restore

	/// <summary>
	/// Creates an account phone number.
	/// </summary>
	/// <param name="realmId">The realm that owns the phone row.</param>
	/// <param name="number">The phone number.</param>
	/// <param name="normalizedNumber">The normalized phone number.</param>
	/// <param name="isPrimary">Whether this is the primary phone.</param>
	/// <param name="isVerified">Whether this phone is verified.</param>
	public UserAccountPhone(
		string realmId,
		string number,
		string normalizedNumber,
		bool isPrimary,
		bool isVerified)
	{
		RealmId = realmId;
		Number = number;
		NormalizedNumber = normalizedNumber;
		IsPrimary = isPrimary;
		IsVerified = isVerified;
	}

	/// <summary>
	/// Gets the realm that owns this phone row.
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
	/// Gets the phone number as provided by the account flow.
	/// </summary>
	public string Number { get; private set; } = string.Empty;

	/// <summary>
	/// Gets the normalized phone number used for comparisons.
	/// </summary>
	public string NormalizedNumber { get; private set; } = string.Empty;

	/// <summary>
	/// Gets whether this is the account primary phone.
	/// </summary>
	public bool IsPrimary { get; private set; }

	/// <summary>
	/// Gets whether this phone has been verified.
	/// </summary>
	public bool IsVerified { get; private set; }

	/// <summary>
	/// Attaches this phone to its owning aggregate.
	/// </summary>
	/// <param name="account">The owner account.</param>
	internal void AttachTo(UserAccount account)
	{
		RealmId = account.RealmId;
		UserAccountId = account.Id;
		UserAccount = account;
	}

	/// <summary>
	/// Marks whether this phone is the account primary phone.
	/// </summary>
	/// <param name="primary">Whether this phone is primary.</param>
	internal virtual void MarkPrimary(bool primary)
	{
		IsPrimary = primary;
	}

	/// <summary>
	/// Marks this phone as verified.
	/// </summary>
	internal void MarkVerified()
	{
		IsVerified = true;
	}
}
