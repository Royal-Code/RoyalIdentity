using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using RoyalIdentity.Storage.EntityFramework.Extensions;
using RoyalIdentity.Storage.EntityFramework.PostgreSql;

namespace Tests.Storage.Configuration.Support;

/// <summary>
/// Isolated PostgreSQL database for one contract scenario. A distinct database allows the opt-in suites to
/// run in parallel without sharing Configuration rows; the database is force-dropped during disposal.
/// </summary>
internal sealed class PostgreSqlConfigurationDatabase
	: IConfigurationTestDatabase<ConfigurationPostgreSqlDbContext>
{
	private readonly string administrativeConnectionString;
	private readonly string databaseName;

	private PostgreSqlConfigurationDatabase(
		string administrativeConnectionString,
		string databaseName,
		string connectionString)
	{
		this.administrativeConnectionString = administrativeConnectionString;
		this.databaseName = databaseName;
		ConnectionString = connectionString;
	}

	public string ConnectionString { get; }

	public static async Task<PostgreSqlConfigurationDatabase> CreateMigratedAsync()
	{
		var administrativeConnectionString = ConfigurationPostgreSqlTestEnvironment.ConnectionString;
		var databaseName = $"royalidentity_configuration_{Guid.NewGuid():N}";
		await using (var connection = new NpgsqlConnection(administrativeConnectionString))
		{
			await connection.OpenAsync();
			await using var command = new NpgsqlCommand($"CREATE DATABASE \"{databaseName}\"", connection);
			await command.ExecuteNonQueryAsync();
		}

		var builder = new NpgsqlConnectionStringBuilder(administrativeConnectionString)
		{
			Database = databaseName,
			Pooling = false,
		};
		var database = new PostgreSqlConfigurationDatabase(
			administrativeConnectionString,
			databaseName,
			builder.ConnectionString);

		try
		{
			await using var context = database.NewContext();
			await context.Database.MigrateAsync();
			return database;
		}
		catch
		{
			await database.DisposeAsync();
			throw;
		}
	}

	public ConfigurationPostgreSqlDbContext NewContext()
		=> new(new DbContextOptionsBuilder<ConfigurationPostgreSqlDbContext>()
			.UseNpgsql(ConnectionString)
			.Options);

	public void AddStorage(ServiceCollection services)
	{
		services.AddDbContext<ConfigurationPostgreSqlDbContext>(options => options.UseNpgsql(ConnectionString));
		services.AddEntityFrameworkConfigurationStorage<ConfigurationPostgreSqlDbContext>();
	}

	public async ValueTask DisposeAsync()
	{
		await using var connection = new NpgsqlConnection(administrativeConnectionString);
		await connection.OpenAsync();
		await using var command = new NpgsqlCommand(
			$"DROP DATABASE IF EXISTS \"{databaseName}\" WITH (FORCE)",
			connection);
		await command.ExecuteNonQueryAsync();
	}
}
