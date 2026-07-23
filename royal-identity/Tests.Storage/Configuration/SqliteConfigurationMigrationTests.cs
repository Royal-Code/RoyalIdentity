using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Tests.Storage.Configuration.Support;

namespace Tests.Storage.Configuration;

/// <summary>
/// Proves the checked-in SQLite migration (plan Fase 2) creates exactly the Configuration schema — no
/// resources/Operational tables — over a real database, applied by <c>MigrateAsync</c> after
/// <c>EnsureDeleted</c> (never <c>EnsureCreated</c>), and that the migration history records it.
/// </summary>
public class SqliteConfigurationMigrationTests
{
	private static readonly string[] ExpectedTables =
	[
		"client_claims",
		"client_secrets",
		"client_string_values",
		"clients",
		"realms",
		"server_options",
		"signing_keys",
	];

	[Fact]
	public async Task Migrate_RecordsInitialConfigurationInHistory()
	{
		await using var database = await SqliteConfigurationDatabase.CreateMigratedAsync();
		await using var context = database.NewContext();

		var applied = await context.Database.GetAppliedMigrationsAsync();

		Assert.Contains(applied, id => id.EndsWith("_InitialConfiguration", StringComparison.Ordinal));
		Assert.Empty(await context.Database.GetPendingMigrationsAsync());
	}

	[Fact]
	public async Task Migrate_CreatesOnlyTheConfigurationTables()
	{
		await using var database = await SqliteConfigurationDatabase.CreateMigratedAsync();

		var tables = new List<string>();
		await using (var command = database.Connection.CreateCommand())
		{
			command.CommandText =
				"SELECT name FROM sqlite_master WHERE type = 'table' AND name NOT LIKE 'sqlite_%' " +
				"AND name NOT IN ('__EFMigrationsHistory', '__EFMigrationsLock') ORDER BY name;";
			await using var reader = await command.ExecuteReaderAsync();
			while (await reader.ReadAsync())
				tables.Add(reader.GetString(0));
		}

		Assert.Equal(ExpectedTables, tables);
	}

	[Fact]
	public async Task Migrate_EnforcesServerOptionsSingletonCheckConstraint()
	{
		await using var database = await SqliteConfigurationDatabase.CreateMigratedAsync();
		await using var context = database.NewContext();

		// The singleton check constraint (id = 1) is part of the migration; a second id must be rejected.
		// Values are bound as parameters so the JSON braces are never read as string.Format placeholders.
		var exception = await Assert.ThrowsAsync<SqliteException>(() =>
			context.Database.ExecuteSqlRawAsync(
				"INSERT INTO server_options (id, payload_version, payload_json, updated_at_utc) VALUES (2, 1, {0}, {1});",
				"{}", "2026-07-22"));

		Assert.Equal(19, exception.SqliteErrorCode); // SQLITE_CONSTRAINT
	}

	[Fact]
	public async Task GeneratedSqlScript_ExecutesAgainstEmptyDatabase()
	{
		var scriptPath = Path.Combine(
			FindRepositoryRoot(),
			"scripts", "sql", "configuration", "sqlite", "0001_initial_configuration.sql");
		var script = await File.ReadAllTextAsync(scriptPath);
		await using var connection = new SqliteConnection("Data Source=:memory:");
		await connection.OpenAsync();
		await using var command = connection.CreateCommand();
		command.CommandText = script;

		await command.ExecuteNonQueryAsync();

		command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'signing_keys';";
		Assert.Equal(1L, await command.ExecuteScalarAsync());
	}

	private static string FindRepositoryRoot()
	{
		var directory = new DirectoryInfo(AppContext.BaseDirectory);
		while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "RoyalIdentity.sln")))
			directory = directory.Parent;

		return directory?.FullName
			?? throw new InvalidOperationException("Could not locate the repository root.");
	}
}
