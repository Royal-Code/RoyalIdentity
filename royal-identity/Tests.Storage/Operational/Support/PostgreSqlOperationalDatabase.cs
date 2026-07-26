using Microsoft.EntityFrameworkCore;
using Npgsql;
using RoyalIdentity.Storage.EntityFramework.PostgreSql;
using Tests.Storage.Configuration;

namespace Tests.Storage.Operational.Support;

/// <summary>
/// An isolated PostgreSQL database holding <b>both</b> families, created by the checked-in migrations — never
/// <c>EnsureCreated</c>. Sharing one database is the interesting case: it is what forces Configuration and
/// Operational to keep separate migrations histories, which on PostgreSQL means the same table name in the
/// <c>configuration</c> and <c>operation</c> schemas (plan DF23). Each fixture applies both with its own
/// configured history, so a drift would surface here.
/// <para>
/// A distinct database per scenario lets the opt-in suites run without sharing rows; it is force-dropped on
/// disposal.
/// </para>
/// </summary>
internal sealed class PostgreSqlOperationalDatabase : IAsyncDisposable
{
    private readonly string administrativeConnectionString;
    private readonly string databaseName;

    private PostgreSqlOperationalDatabase(
        string administrativeConnectionString, string databaseName, string connectionString)
    {
        this.administrativeConnectionString = administrativeConnectionString;
        this.databaseName = databaseName;
        ConnectionString = connectionString;
    }

    public string ConnectionString { get; }

    /// <summary>Creates an empty isolated database, without applying any migration.</summary>
    public static async Task<PostgreSqlOperationalDatabase> CreateEmptyAsync()
    {
        var administrativeConnectionString = StoragePostgreSqlTestEnvironment.ConnectionString;
        var databaseName = $"royalidentity_operational_{Guid.NewGuid():N}";

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

        return new PostgreSqlOperationalDatabase(
            administrativeConnectionString, databaseName, builder.ConnectionString);
    }

    public static async Task<PostgreSqlOperationalDatabase> CreateMigratedAsync()
    {
        var database = await CreateEmptyAsync();

        try
        {
            await using (var configuration = database.NewConfigurationContext())
                await configuration.Database.MigrateAsync();

            await using (var operational = database.NewOperationalContext())
                await operational.Database.MigrateAsync();

            return database;
        }
        catch
        {
            await database.DisposeAsync();
            throw;
        }
    }

    public ConfigurationPostgreSqlDbContext NewConfigurationContext()
        => new(new DbContextOptionsBuilder<ConfigurationPostgreSqlDbContext>()
            .UseNpgsql(ConnectionString, npgsql => npgsql.UseConfigurationMigrationsHistory())
            .Options);

    public OperationalPostgreSqlDbContext NewOperationalContext()
        => new(new DbContextOptionsBuilder<OperationalPostgreSqlDbContext>()
            .UseNpgsql(ConnectionString, npgsql => npgsql.UseOperationalMigrationsHistory())
            .Options);

    public async ValueTask DisposeAsync()
    {
        await using var connection = new NpgsqlConnection(administrativeConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            $"DROP DATABASE IF EXISTS \"{databaseName}\" WITH (FORCE)", connection);
        await command.ExecuteNonQueryAsync();
    }
}
