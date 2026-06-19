using RoyalCode.Entities;

namespace RoyalIdentity.UserAccounts.Features.Accounts.Domain;

/// <summary>
/// Role assigned directly to a user account.
/// </summary>
public class UserAccountRole : Entity<long>
{
#nullable disable
	/// <summary>
	/// Constructor for EF Core.
	/// </summary>
	protected UserAccountRole()
	{
	}
#nullable restore

	/// <summary>
	/// Creates an account role.
	/// </summary>
	/// <param name="realmId">The realm that owns the role row.</param>
	/// <param name="name">The role name.</param>
	/// <param name="normalizedName">The normalized role name.</param>
	public UserAccountRole(string realmId, string name, string normalizedName)
	{
		RealmId = realmId;
		Name = name;
		NormalizedName = normalizedName;
	}

	/// <summary>
	/// Gets the realm that owns this role row.
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
	/// Gets the role name.
	/// </summary>
	public string Name { get; private set; } = string.Empty;

	/// <summary>
	/// Gets the normalized role name used for comparisons.
	/// </summary>
	public string NormalizedName { get; private set; } = string.Empty;

	/// <summary>
	/// Attaches this role to its owning aggregate.
	/// </summary>
	/// <param name="account">The owner account.</param>
	internal void AttachTo(UserAccount account)
	{
		RealmId = account.RealmId;
		UserAccountId = account.Id;
		UserAccount = account;
	}
}
