using RoyalIdentity.UserAccounts.Options;

namespace RoyalIdentity.UserAccounts.Integration;

/// <summary>
/// Resolves UserAccounts policies from a realm identifier.
/// </summary>
public interface IUserAccountsRealmOptionsResolver
{
	/// <summary>
	/// Resolves an independent options instance for the realm.
	/// </summary>
	UserAccountsRealmOptions Resolve(string realmId);
}
