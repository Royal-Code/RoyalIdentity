using RoyalIdentity.Models;
using RoyalIdentity.Options;
using RoyalIdentity.UserAccounts.Integration;
using RoyalIdentity.UserAccounts.Options;

namespace Tests.UserAccounts;

public class UserAccountsRealmBindingFactoryTests
{
	[Fact]
	public void Create_TranslatesRealmToRealmIdAndIndependentOptions()
	{
		var defaults = new UserAccountsRealmOptions
		{
			AllowRegistration = true
		};
		defaults.PasswordOptions.MinimumLength = 12;

		var resolver = new DefaultUserAccountsRealmOptionsResolver(defaults);
		var factory = new UserAccountsRealmBindingFactory(resolver);
		var realm = new Realm(
			"realm-1",
			"realm.example",
			"realm",
			"Realm",
			@internal: false,
			new RealmOptions(new ServerOptions()));

		var binding = factory.Create(realm);

		defaults.AllowRegistration = false;
		defaults.PasswordOptions.MinimumLength = 6;

		Assert.Equal("realm-1", binding.RealmId);
		Assert.True(binding.Options.AllowRegistration);
		Assert.Equal(12, binding.Options.PasswordOptions.MinimumLength);
	}

	[Fact]
	public void Create_RejectsInvalidResolvedOptions()
	{
		var defaults = new UserAccountsRealmOptions
		{
			LoginWithEmail = true,
			AllowDuplicateEmail = true
		};
		var factory = new UserAccountsRealmBindingFactory(new DefaultUserAccountsRealmOptionsResolver(defaults));
		var realm = new Realm(
			"realm-1",
			"realm.example",
			"realm",
			"Realm",
			@internal: false,
			new RealmOptions(new ServerOptions()));

		var ex = Assert.Throws<InvalidOperationException>(() => factory.Create(realm));

		Assert.Contains("Email login", ex.Message, StringComparison.Ordinal);
	}
}
