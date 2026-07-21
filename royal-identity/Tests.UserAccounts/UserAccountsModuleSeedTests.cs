using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RoyalIdentity.Storage.InMemory;
using RoyalIdentity.UserAccounts.Features.Accounts.Domain;
using RoyalIdentity.UserAccounts.Features.ScopeProperties.Domain;
using RoyalIdentity.UserAccounts.Infrastructure.Data;
using RoyalIdentity.UserAccounts.Integration;
using RoyalIdentity.UserAccounts.Options;
using RoyalIdentity.UserAccounts.Sqlite;
using RoyalIdentity.Users.Contracts;
using RoyalIdentity.Users.Defaults;

namespace Tests.UserAccounts;

public class UserAccountsModuleSeedTests
{
	private const string RealmId = "seed-idempotence";
	private static readonly DateTimeOffset Now = new(2026, 7, 20, 12, 0, 0, TimeSpan.Zero);

	[Fact]
	public async Task SeedDefaults_TwiceInDifferentScopes_PreservesASingleDefaultGraph()
	{
		var options = UserAccountsTestOptions.Relaxed(minimumLength: 1, allowProvidedSubjectId: true);
		var services = new ServiceCollection();
		services.AddSingleton<TimeProvider>(new FixedClock(Now));
		services.AddTransient<IPasswordProtector, DefaultPasswordProtector>();
		services.AddSingleton<IUserAccountsRealmOptionsResolver>(
			new DefaultUserAccountsRealmOptionsResolver(options));
		services.AddUserAccountsSqliteInMemory();
		services.AddUserAccountsForRoyalIdentity();

		await using var provider = services.BuildServiceProvider(
			new ServiceProviderOptions { ValidateScopes = true });

		for (var attempt = 0; attempt < 2; attempt++)
		{
			using var scope = provider.CreateScope();
			var db = scope.ServiceProvider.GetRequiredService<UserAccountsDbContext>();
			await UserAccountsModuleSeed.SeedDefaultScopesAsync(db, RealmId, Now);
			await UserAccountsModuleSeed.SeedDefaultAccountsAsync(
				scope.ServiceProvider, RealmId, options, Now);
		}

		using var assertScope = provider.CreateScope();
		var assertDb = assertScope.ServiceProvider.GetRequiredService<UserAccountsDbContext>();
		var propertyScopes = await assertDb.PropertyScopes
			.Where(s => s.RealmId == RealmId)
			.OrderBy(s => s.Name)
			.ToListAsync();
		var accounts = await assertDb.UserAccounts
			.Include(a => a.LocalCredential)
			.Include("EmailItems")
			.Include("RoleItems")
			.Where(a => a.RealmId == RealmId)
			.OrderBy(a => a.Username)
			.ToListAsync();

		Assert.Collection(
			propertyScopes,
			scope => AssertDefaultScope(scope, "email"),
			scope => AssertDefaultScope(scope, "profile"));
		Assert.Collection(
			accounts,
			account => AssertDefaultAccount(
				account, MemoryStorage.AliceSubjectId, UserAccountsModuleSeed.AliceUsername,
				"Alice", "Alice@example.com"),
			account => AssertDefaultAccount(
				account, MemoryStorage.BobSubjectId, UserAccountsModuleSeed.BobUsername,
				"Bob", "bob@example.com"));
		Assert.Equal(2, await assertDb.Set<UserAccountCredential>().CountAsync(c => c.RealmId == RealmId));
		Assert.Equal(2, await assertDb.Set<UserAccountEmail>().CountAsync(e => e.RealmId == RealmId));
		Assert.Equal(2, await assertDb.Set<UserAccountRole>().CountAsync(r => r.RealmId == RealmId));
	}

	private static void AssertDefaultScope(PropertyScope scope, string expectedName)
	{
		Assert.Equal(expectedName, scope.Name);
		Assert.True(scope.IsActive);
		Assert.NotNull(scope.ActiveVersionId);
	}

	private static void AssertDefaultAccount(
		UserAccount account,
		string expectedSubjectId,
		string expectedUsername,
		string expectedDisplayName,
		string expectedEmail)
	{
		Assert.Equal(expectedSubjectId, account.SubjectId);
		Assert.Equal(expectedUsername, account.Username);
		Assert.Equal(expectedDisplayName, account.DisplayName);
		Assert.True(account.IsActive);
		Assert.True(account.LocalCredential.HasPassword);
		var email = Assert.Single(account.Emails);
		Assert.Equal(expectedEmail, email.Address);
		Assert.True(email.IsPrimary);
		Assert.True(email.IsVerified);
		var role = Assert.Single(account.Roles);
		Assert.Equal("admin", role.Name);
	}

	private sealed class FixedClock(DateTimeOffset now) : TimeProvider
	{
		public override DateTimeOffset GetUtcNow() => now;
	}
}
