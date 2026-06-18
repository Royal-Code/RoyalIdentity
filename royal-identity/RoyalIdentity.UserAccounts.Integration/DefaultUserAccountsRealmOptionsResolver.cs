using RoyalIdentity.UserAccounts.Options;

namespace RoyalIdentity.UserAccounts.Integration;

/// <summary>
/// Temporary resolver used before module-owned persistent options exist.
/// </summary>
public class DefaultUserAccountsRealmOptionsResolver : IUserAccountsRealmOptionsResolver
{
	private readonly UserAccountsRealmOptions defaults;

	/// <summary>
	/// Creates a resolver using module defaults.
	/// </summary>
	public DefaultUserAccountsRealmOptionsResolver()
		: this(new UserAccountsRealmOptions())
	{
	}

	/// <summary>
	/// Creates a resolver using a copy of the supplied default options.
	/// </summary>
	public DefaultUserAccountsRealmOptionsResolver(UserAccountsRealmOptions defaults)
	{
		this.defaults = new UserAccountsRealmOptions(defaults);
	}

	/// <inheritdoc />
	public UserAccountsRealmOptions Resolve(string realmId)
	{
		return new UserAccountsRealmOptions(defaults);
	}
}
