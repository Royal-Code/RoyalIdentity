using Microsoft.EntityFrameworkCore;
using Npgsql;
using RoyalIdentity.Migrations;
using RoyalIdentity.Storage.EntityFramework.PostgreSql;

namespace Tests.Storage.Configuration;

public class PostgreSqlConfigurationMigrationTests
{
	[ConfigurationPostgreSqlFact]
	[Trait("Category", "PostgreSql")]
	public async Task Runner_Twice_CreatesEquivalentSchemaAndIdempotentProductSeed()
	{
		var options = new MigrationRunnerOptions
		{
			ConfigurationProvider = ConfigurationDatabaseProvider.PostgreSql,
			ConfigurationConnection = ConfigurationPostgreSqlTestEnvironment.ConnectionString,
			Seed = ConfigurationSeedMode.Product,
			KeyProtector = ConfigurationKeyProtector.Plain,
		};

		await ConfigurationMigrationRunner.RunAsync(options);
		await ConfigurationMigrationRunner.RunAsync(options);

		await using var context = new ConfigurationPostgreSqlDbContext(
			new DbContextOptionsBuilder<ConfigurationPostgreSqlDbContext>()
				.UseNpgsql(ConfigurationPostgreSqlTestEnvironment.ConnectionString)
				.Options);
		Assert.Empty(await context.Database.GetPendingMigrationsAsync());
		Assert.Equal(3, await context.Realms.CountAsync());
		Assert.Equal(1, await context.Clients.CountAsync(client => client.ClientId == "server_admin"));
		Assert.Equal(3, await context.SigningKeys.CountAsync());

		await using var connection = new NpgsqlConnection(ConfigurationPostgreSqlTestEnvironment.ConnectionString);
		await connection.OpenAsync();
		var script = await File.ReadAllTextAsync(Path.Combine(
			FindRepositoryRoot(),
			"scripts", "sql", "configuration", "postgresql", "0001_initial_configuration.sql"));
		await using (var scriptCommand = new NpgsqlCommand(script, connection) { CommandTimeout = 60 })
		{
			// The production script is idempotent: once migration history is present, repeated execution is a no-op.
			await scriptCommand.ExecuteNonQueryAsync();
			await scriptCommand.ExecuteNonQueryAsync();
		}

		Assert.Equal("jsonb", await ScalarAsync(connection,
			"SELECT data_type FROM information_schema.columns " +
			"WHERE table_schema = 'configuration' AND table_name = 'server_options' AND column_name = 'payload_json'"));
		Assert.Equal("C", await ScalarAsync(connection,
			"SELECT collation_name FROM information_schema.columns " +
			"WHERE table_schema = 'configuration' AND table_name = 'realms' AND column_name = 'domain'"));
		Assert.Equal("ux_realms_domain", await ScalarAsync(connection,
			"SELECT indexname FROM pg_indexes " +
			"WHERE schemaname = 'configuration' AND tablename = 'realms' AND indexname = 'ux_realms_domain'"));
	}

	private static async Task<string?> ScalarAsync(NpgsqlConnection connection, string sql)
	{
		await using var command = new NpgsqlCommand(sql, connection);
		return (string?)await command.ExecuteScalarAsync();
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

internal static class ConfigurationPostgreSqlTestEnvironment
{
	public const string ConnectionStringVariable = "ROYALIDENTITY_CONFIGURATION_TEST_POSTGRES";

	public static string ConnectionString =>
		Environment.GetEnvironmentVariable(ConnectionStringVariable)
		?? throw new InvalidOperationException(
			$"Environment variable {ConnectionStringVariable} is required for PostgreSQL tests.");
}

[AttributeUsage(AttributeTargets.Method)]
internal sealed class ConfigurationPostgreSqlFactAttribute : FactAttribute
{
	public ConfigurationPostgreSqlFactAttribute()
	{
		if (string.IsNullOrWhiteSpace(
			Environment.GetEnvironmentVariable(ConfigurationPostgreSqlTestEnvironment.ConnectionStringVariable)))
		{
			Skip = $"Set {ConfigurationPostgreSqlTestEnvironment.ConnectionStringVariable} or run " +
				"scripts/Test-ConfigurationPostgreSql.ps1 to execute PostgreSQL tests.";
		}
	}
}
