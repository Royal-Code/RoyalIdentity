using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using RoyalIdentity.UserAccounts.Features.Accounts.Domain;
using RoyalIdentity.UserAccounts.Options;
using RoyalIdentity.UserAccounts.PostgreSql;

namespace Tests.UserAccounts;

/// <summary>
/// Applies the real PostgreSQL migration when an external test database is explicitly provided. The normal test
/// suite remains infrastructure-free; <c>scripts/Test-UserAccountsPostgreSql.ps1</c> prepares an ephemeral database
/// and sets the required connection string for this test.
/// </summary>
public class UserAccountsPostgreSqlMigrationTests
{
	private static readonly DateTimeOffset Now = new(2026, 7, 20, 10, 0, 0, TimeSpan.Zero);

	[PostgreSqlFact]
	[Trait("Category", "PostgreSql")]
	public async Task Migrate_CreatesFunctionalSchema_AndUsesXminForConcurrency()
	{
		await using var provider = BuildProvider(PostgreSqlTestEnvironment.ConnectionString);
		long accountId;

		using (var migration = NewScope(provider))
		{
			await migration.Db.Database.MigrateAsync();
			var applied = await migration.Db.Database.GetAppliedMigrationsAsync();
			Assert.Contains(applied, name => name.EndsWith("_InitialCreate", StringComparison.Ordinal));
		}

		using (var seed = NewScope(provider))
		{
			var account = new UserAccount("realm-a", "subject-1", "alice", "ALICE", "Alice", Now);
			Assert.True(account.AddEmail(
				new UserAccountEmail("realm-a", "alice@example.com", "ALICE@EXAMPLE.COM", true, true, false), Now).IsSuccess);
			account.SetPassword("hashed-secret", Now, new PasswordOptions());

			seed.Db.UserAccounts.Add(account);
			await seed.Db.SaveChangesAsync();
			accountId = account.Id;

			Assert.True(accountId > 0);
			Assert.True(account.Version > 0);
		}

		using (var uniqueIndex = NewScope(provider))
		{
			var exception = await Assert.ThrowsAsync<PostgresException>(() =>
				uniqueIndex.Db.Database.ExecuteSqlRawAsync(
					"""
					INSERT INTO "UserAccountEmails"
						("RealmId", "UserAccountId", "Address", "NormalizedAddress", "IsPrimary", "IsVerified", "IsFictitious")
					VALUES
						({0}, {1}, {2}, {3}, TRUE, TRUE, FALSE)
					""",
					"realm-a", accountId, "alice.alt@example.com", "ALICE.ALT@EXAMPLE.COM"));

			Assert.Equal(PostgresErrorCodes.UniqueViolation, exception.SqlState);
			Assert.Equal("UX_UserAccountEmails_PrimaryPerAccount", exception.ConstraintName);
		}

		using var first = NewScope(provider);
		using var second = NewScope(provider);
		var firstAccount = await first.Db.UserAccounts.SingleAsync(a => a.Id == accountId);
		var secondAccount = await second.Db.UserAccounts.SingleAsync(a => a.Id == accountId);
		var versionBeforeUpdate = firstAccount.Version;

		Assert.True(firstAccount.ChangeDisplayName("Alice First", Now.AddMinutes(1)).IsSuccess);
		await first.Db.SaveChangesAsync();
		Assert.NotEqual(versionBeforeUpdate, firstAccount.Version);

		Assert.True(secondAccount.ChangeDisplayName("Alice Second", Now.AddMinutes(2)).IsSuccess);
		await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.Db.SaveChangesAsync());
	}

	private static ServiceProvider BuildProvider(string connectionString)
	{
		var configuration = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["ConnectionStrings:UserAccountsPostgreSqlTests"] = connectionString
			})
			.Build();

		var services = new ServiceCollection();
		services.AddSingleton<IConfiguration>(configuration);
		services.AddUserAccountsPostgreSql("UserAccountsPostgreSqlTests");
		return services.BuildServiceProvider();
	}

	private static Scope NewScope(ServiceProvider provider) => new(provider.CreateScope());

	private readonly struct Scope(IServiceScope serviceScope) : IDisposable
	{
		public UserAccountsPostgreSqlDbContext Db { get; } =
			serviceScope.ServiceProvider.GetRequiredService<UserAccountsPostgreSqlDbContext>();

		public void Dispose() => serviceScope.Dispose();
	}
}

internal static class PostgreSqlTestEnvironment
{
	public const string ConnectionStringVariable = "ROYALIDENTITY_TEST_POSTGRES";

	public static string ConnectionString =>
		Environment.GetEnvironmentVariable(ConnectionStringVariable)
		?? throw new InvalidOperationException(
			$"Environment variable {ConnectionStringVariable} is required for PostgreSQL tests.");
}

[AttributeUsage(AttributeTargets.Method)]
internal sealed class PostgreSqlFactAttribute : FactAttribute
{
	public PostgreSqlFactAttribute()
	{
		if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(PostgreSqlTestEnvironment.ConnectionStringVariable)))
		{
			Skip = $"Set {PostgreSqlTestEnvironment.ConnectionStringVariable} or run " +
				"scripts/Test-UserAccountsPostgreSql.ps1 to execute PostgreSQL tests.";
		}
	}
}
