using RoyalIdentity.Models;

namespace RoyalIdentity.UserAccounts.Integration;

/// <summary>
/// Translates the IdP realm boundary into the primitive UserAccounts realm binding.
/// </summary>
public class UserAccountsRealmBindingFactory(IUserAccountsRealmOptionsResolver optionsResolver)
{
	/// <summary>
	/// Creates a binding for the supplied IdP realm.
	/// </summary>
	public UserAccountsRealmBinding Create(Realm realm)
	{
		var options = optionsResolver.Resolve(realm.Id);
		options.EnsureValid();

		return new UserAccountsRealmBinding(realm.Id, options);
	}
}
