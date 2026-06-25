using RoyalCode.Entities;

namespace RoyalIdentity.UserAccounts.Features.Accounts.Domain;

/// <summary>
/// Email address owned by a user account.
/// </summary>
public class UserAccountEmail : Entity<long>
{
#nullable disable
	/// <summary>
	/// Constructor for EF Core.
	/// </summary>
	protected UserAccountEmail()
	{
	}
#nullable restore

	/// <summary>
	/// Creates an account email.
	/// </summary>
	/// <param name="realmId">The realm that owns the email row.</param>
	/// <param name="address">The email address.</param>
	/// <param name="normalizedAddress">The normalized email address.</param>
	/// <param name="isPrimary">Whether this is the primary email.</param>
	/// <param name="isVerified">Whether this email is verified.</param>
	/// <param name="isFictitious">Whether this email is generated and not user-owned.</param>
	public UserAccountEmail(
		string realmId,
		string address,
		string normalizedAddress,
		bool isPrimary,
		bool isVerified,
		bool isFictitious)
	{
		RealmId = realmId;
		Address = address;
		NormalizedAddress = normalizedAddress;
		IsPrimary = isPrimary;
		IsVerified = isVerified;
		IsFictitious = isFictitious;
	}

	/// <summary>
	/// Gets the realm that owns this email row.
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
	/// Gets the email address as provided by the account flow.
	/// </summary>
	public string Address { get; private set; } = string.Empty;

	/// <summary>
	/// Gets the normalized email address used for comparisons.
	/// </summary>
	public string NormalizedAddress { get; private set; } = string.Empty;

	/// <summary>
	/// Gets whether this is the account primary email.
	/// </summary>
	public bool IsPrimary { get; private set; }

	/// <summary>
	/// Gets whether this email has been verified.
	/// </summary>
	public bool IsVerified { get; private set; }

	/// <summary>
	/// Gets whether this email is fictitious.
	/// </summary>
	public bool IsFictitious { get; private set; }

	/// <summary>
	/// Attaches this email to its owning aggregate.
	/// </summary>
	/// <param name="account">The owner account.</param>
	internal void AttachTo(UserAccount account)
	{
		RealmId = account.RealmId;
		UserAccountId = account.Id;
		UserAccount = account;
	}

	/// <summary>
	/// Marks whether this email is the account primary email.
	/// </summary>
	/// <param name="primary">Whether this email is primary.</param>
	internal virtual void MarkPrimary(bool primary)
	{
		IsPrimary = primary;
	}

	/// <summary>
	/// Marks this email as verified.
	/// </summary>
	internal void MarkVerified()
	{
		IsVerified = true;
	}
}
