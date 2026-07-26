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

    /// <summary>Creates an empty isolated database, without applying any migration.</summary>
    public static async Task<PostgreSqlConfigurationDatabase> CreateEmptyAsync()
    {
        var administrativeConnectionString = StoragePostgreSqlTestEnvironment.ConnectionString;
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

        return new PostgreSqlConfigurationDatabase(
            administrativeConnectionString,
            databaseName,
            builder.ConnectionString);
    }

    public static async Task<PostgreSqlConfigurationDatabase> CreateMigratedAsync()
    {
        var database = await CreateEmptyAsync();

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

    // DF23: every place that builds options for migrations configures the history explicitly — the entities'
    // schema does not imply it. A fixture that skipped this would migrate into public."__EFMigrationsHistory"
    // and stay green while proving the opposite of what it claims.
    public ConfigurationPostgreSqlDbContext NewContext()
        => new(new DbContextOptionsBuilder<ConfigurationPostgreSqlDbContext>()
            .UseNpgsql(ConnectionString, npgsql => npgsql.UseConfigurationMigrationsHistory())
            .Options);

    public void AddStorage(ServiceCollection services)
    {
        services.AddDbContext<ConfigurationPostgreSqlDbContext>(options => options
            .UseNpgsql(ConnectionString, npgsql => npgsql.UseConfigurationMigrationsHistory()));
        services.AddEntityFrameworkConfigurationStorage<ConfigurationPostgreSqlDbContext>();
    }

    /// <summary>Whether a migrations history table exists in the given schema, so a fixture can prove where it is.</summary>
    public async Task<bool> HasMigrationsHistoryAsync(string schema)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "SELECT COUNT(*) FROM information_schema.tables " +
            "WHERE table_schema = @schema AND table_name = '__EFMigrationsHistory'",
            connection);
        command.Parameters.AddWithValue("schema", schema);

        return Convert.ToInt64(await command.ExecuteScalarAsync()) > 0;
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
