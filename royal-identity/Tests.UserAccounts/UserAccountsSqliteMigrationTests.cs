using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RoyalIdentity.UserAccounts.Features.Accounts.Domain;
using RoyalIdentity.UserAccounts.Infrastructure.Data;
using RoyalIdentity.UserAccounts.Options;
using RoyalIdentity.UserAccounts.Sqlite;

namespace Tests.UserAccounts;

/// <summary>
/// Fase 2 (plan-users-accounts-sqlite-hardening.md, Q6) — the rest of the suite uses `EnsureCreated()` (fast,
/// no migration fidelity check). These tests instead apply the real `Migrations/` via `Database.MigrateAsync()`
/// against a shared in-memory SQLite connection, proving the generated schema is functionally equivalent: a
/// round-trip persists correctly, the partial unique indexes (primary email/phone per account) are actually
/// created, and the `Version` concurrency token still rejects a stale concurrent update.
/// </summary>
public class UserAccountsSqliteMigrationTests
{
	private static readonly DateTimeOffset Now = new(2026, 6, 19, 10, 0, 0, TimeSpan.Zero);

	[Fact]
	public async Task Migrate_CreatesFunctionalSchema_RoundTripsAccountWithEmailAndCredential()
	{
		await using var provider = await BuildMigratedProviderAsync();
		long accountId;

		using (var write = NewScope(provider))
		{
			var account = new UserAccount("realm-a", "subject-1", "alice", "ALICE", "Alice", Now);
			Assert.True(account.AddEmail(
				new UserAccountEmail("realm-a", "alice@example.com", "ALICE@EXAMPLE.COM", true, true, false), Now).IsSuccess);
			account.SetPassword("hashed-secret", Now, new PasswordOptions());

			write.Db.UserAccounts.Add(account);
			await write.Db.SaveChangesAsync();
			accountId = account.Id;
		}

		Assert.True(accountId > 0);

		using var read = NewScope(provider);
		var loaded = await read.Db.UserAccounts
			.Include(a => a.LocalCredential)
			.Include("EmailItems")
			.SingleAsync(a => a.Id == accountId);

		Assert.Equal("hashed-secret", loaded.LocalCredential.PasswordHash);
		Assert.Equal("alice@example.com", loaded.PrimaryEmail?.Address);
	}

	[Fact]
	public async Task Migrate_CreatesPartialUniqueIndex_RejectsSecondPrimaryEmailForSameAccount()
	{
		// The migration must reproduce UX_UserAccountEmails_PrimaryPerAccount (a filtered unique index) —
		// hand-written EF mappings for filtered indexes are the easiest thing to get wrong in a migration.
		await using var provider = await BuildMigratedProviderAsync();
		long accountId;

		using (var write = NewScope(provider))
		{
			var account = new UserAccount("realm-a", "subject-1", "alice", "ALICE", "Alice", Now);
			write.Db.UserAccounts.Add(account);
			await write.Db.SaveChangesAsync();
			accountId = account.Id;
		}

		using var ctx = NewScope(provider);
		await ctx.Db.Database.ExecuteSqlRawAsync(
			"""
			INSERT INTO "UserAccountEmails"
				("RealmId", "UserAccountId", "Address", "NormalizedAddress", "IsPrimary", "IsVerified", "IsFictitious")
			VALUES
				({0}, {1}, {2}, {3}, 1, 1, 0)
			""",
			"realm-a", accountId, "alice@example.com", "ALICE@EXAMPLE.COM");

		var exception = await Assert.ThrowsAsync<SqliteException>(() =>
			ctx.Db.Database.ExecuteSqlRawAsync(
				"""
				INSERT INTO "UserAccountEmails"
					("RealmId", "UserAccountId", "Address", "NormalizedAddress", "IsPrimary", "IsVerified", "IsFictitious")
				VALUES
					({0}, {1}, {2}, {3}, 1, 1, 0)
				""",
				"realm-a", accountId, "alice.alt@example.com", "ALICE.ALT@EXAMPLE.COM"));

		Assert.Equal(19, exception.SqliteErrorCode); // SQLITE_CONSTRAINT
	}

	[Fact]
	public async Task Migrate_CreatesPartialUniqueIndex_RejectsSecondPrimaryPhoneForSameAccount()
	{
		await using var provider = await BuildMigratedProviderAsync();
		long accountId;

		using (var write = NewScope(provider))
		{
			var account = new UserAccount("realm-a", "subject-1", "alice", "ALICE", "Alice", Now);
			write.Db.UserAccounts.Add(account);
			await write.Db.SaveChangesAsync();
			accountId = account.Id;
		}

		using var ctx = NewScope(provider);
		await ctx.Db.Database.ExecuteSqlRawAsync(
			"""
			INSERT INTO "UserAccountPhones"
				("RealmId", "UserAccountId", "Number", "NormalizedNumber", "IsPrimary", "IsVerified")
			VALUES
				({0}, {1}, {2}, {3}, 1, 1)
			""",
			"realm-a", accountId, "+55 11 5555-0100", "+551155550100");

		var exception = await Assert.ThrowsAsync<SqliteException>(() =>
			ctx.Db.Database.ExecuteSqlRawAsync(
				"""
				INSERT INTO "UserAccountPhones"
					("RealmId", "UserAccountId", "Number", "NormalizedNumber", "IsPrimary", "IsVerified")
				VALUES
					({0}, {1}, {2}, {3}, 1, 1)
				""",
				"realm-a", accountId, "+55 11 5555-0101", "+551155550101"));

		Assert.Equal(19, exception.SqliteErrorCode); // SQLITE_CONSTRAINT
	}

	[Fact]
	public async Task Migrate_PreservesVersionAsConcurrencyToken_RejectsStaleConcurrentUpdate()
	{
		await using var provider = await BuildMigratedProviderAsync();
		long accountId;

		using (var seed = NewScope(provider))
		{
			var account = new UserAccount("realm-a", "subject-1", "alice", "ALICE", "Alice", Now);
			seed.Db.UserAccounts.Add(account);
			await seed.Db.SaveChangesAsync();
			accountId = account.Id;
		}

		using var first = NewScope(provider);
		using var second = NewScope(provider);
		var firstAccount = await first.Db.UserAccounts.SingleAsync(a => a.Id == accountId);
		var secondAccount = await second.Db.UserAccounts.SingleAsync(a => a.Id == accountId);

		Assert.True(firstAccount.ChangeDisplayName("Alice First", Now.AddMinutes(1)).IsSuccess);
		await first.Db.SaveChangesAsync();

		Assert.True(secondAccount.ChangeDisplayName("Alice Second", Now.AddMinutes(2)).IsSuccess);
		await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.Db.SaveChangesAsync());
	}

	/// <summary>
	/// Builds the module DI graph without the module's own `EnsureDatabaseCreated()` convenience (which the
	/// `AddUserAccountsSqliteInMemory()` wrapper bakes in) so the schema can be created via the real
	/// `Migrations/` instead, over a single shared in-memory SQLite connection (kept alive by the returned
	/// provider's singleton <see cref="SqliteConnection"/>).
	/// </summary>
	private static async Task<ServiceProvider> BuildMigratedProviderAsync()
	{
		var services = new ServiceCollection();
		services.AddSqliteInMemoryWorkContext<UserAccountsSqliteDbContext>().ConfigureUserAccounts();
		var provider = services.BuildServiceProvider();

		using var scope = provider.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<UserAccountsSqliteDbContext>();
		await db.Database.MigrateAsync();

		return provider;
	}

	private static Scope NewScope(ServiceProvider provider) => new(provider.CreateScope());

	private readonly struct Scope(IServiceScope serviceScope) : IDisposable
	{
		public UserAccountsSqliteDbContext Db { get; } = serviceScope.ServiceProvider.GetRequiredService<UserAccountsSqliteDbContext>();

		public void Dispose() => serviceScope.Dispose();
	}
}
