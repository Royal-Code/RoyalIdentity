using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using RoyalIdentity.Migrations;
using RoyalIdentity.Storage.EntityFramework.Extensions;
using RoyalIdentity.Storage.EntityFramework.PostgreSql;

namespace Tests.Integration.Prepare;

/// <summary>
/// The canonical persistent composition with Configuration and Operational moved to a real PostgreSQL database
/// and the <b>durable</b> replay-protection backing declared — the shape the production Server runs
/// (plan-replay-protection Fase 3).
/// <para>
/// UserAccounts deliberately stays on SQLite: no scenario here makes the account backing its subject, and the
/// property under test lives entirely in the Operational family.
/// </para>
/// <para>
/// Opt-in, like every PostgreSQL suite of this repository: without
/// <see cref="PostgreSqlIntegrationTestEnvironment.ConnectionStringVariable"/> the scenarios are skipped, so a
/// solution-wide <c>dotnet test</c> never requires a container runtime.
/// </para>
/// </summary>
public class PostgreSqlReplayProtectionAppFactory : PersistentStorageAppFactory
{
    private readonly string databaseName = $"royalidentity_replay_{Guid.NewGuid():N}";
    private string? connectionString;
    private bool dropped;

    private string ConnectionString =>
        connectionString ?? throw new InvalidOperationException("The database has not been created yet.");

    protected override void ProvisionIdpStorage()
    {
        CreateDatabase();

        EnsureSucceeded(StorageMigrationRunner.RunAsync(new MigrationRunnerOptions
        {
            ConfigurationProvider = ConfigurationDatabaseProvider.PostgreSql,
            Families = StorageFamilySelection.Configuration | StorageFamilySelection.Operational,
            ConfigurationConnection = ConnectionString,
            OperationalConnection = ConnectionString,
            DatabaseTopology = StorageDatabaseTopology.Shared,
            Seed = ConfigurationSeedMode.All,
            ProductSeed = ProductSeedOptions,
            KeyProtector = ConfigurationKeyProtector.DataProtection,
            DataProtectionKeyRing = KeyRingPath,
            DataProtectionApplicationName = DataProtectionApplicationName,
        }).GetAwaiter().GetResult());
    }

    protected override void RegisterIdpStorage(IServiceCollection services)
    {
        services.AddDbContext<ConfigurationPostgreSqlDbContext>(options => options.UseNpgsql(
            ConnectionString,
            npgsql => npgsql.UseConfigurationMigrationsHistory()));
        services.AddDbContext<OperationalPostgreSqlDbContext>(options => options.UseNpgsql(
            ConnectionString,
            npgsql => npgsql.UseOperationalMigrationsHistory()));

        services.AddEntityFrameworkConfigurationStorage<ConfigurationPostgreSqlDbContext>();
        services.AddEntityFrameworkOperationalStorage<OperationalPostgreSqlDbContext>();

        RegisterContextAliases<ConfigurationPostgreSqlDbContext, OperationalPostgreSqlDbContext>(services);
    }

    /// <summary>The point of this fixture: the same backing the Server declares, on the same engine.</summary>
    protected override void RegisterReplayProtection(IServiceCollection services)
        => services.AddOperationalReplayProtection();

    /// <summary>Counts rows over its own connection, so an assertion never reads a change tracker.</summary>
    public async Task<int> CountReplayHandlesAsync()
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "SELECT COUNT(*) FROM operation.replay_handles;", connection);

        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    private void CreateDatabase()
    {
        var administrative = PostgreSqlIntegrationTestEnvironment.ConnectionString;

        using (var connection = new NpgsqlConnection(administrative))
        {
            connection.Open();
            using var command = new NpgsqlCommand($"CREATE DATABASE \"{databaseName}\";", connection);
            command.ExecuteNonQuery();
        }

        connectionString = new NpgsqlConnectionStringBuilder(administrative)
        {
            Database = databaseName,
        }.ConnectionString;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && !dropped && connectionString is not null)
        {
            dropped = true;
            NpgsqlConnection.ClearAllPools();

            try
            {
                using var connection = new NpgsqlConnection(
                    PostgreSqlIntegrationTestEnvironment.ConnectionString);
                connection.Open();
                using var command = new NpgsqlCommand(
                    $"DROP DATABASE IF EXISTS \"{databaseName}\" WITH (FORCE);", connection);
                command.ExecuteNonQuery();
            }
            catch (NpgsqlException)
            {
                // A database left behind on an ephemeral container is not worth failing a suite over.
            }
        }

        base.Dispose(disposing);
    }
}

/// <summary>
/// The opt-in gate for PostgreSQL scenarios of the integration suite. It reads the same variable the storage
/// suites use, so one provisioning script can drive both.
/// </summary>
public static class PostgreSqlIntegrationTestEnvironment
{
    /// <summary>Administrative connection string of the PostgreSQL server the scripts publish.</summary>
    public const string ConnectionStringVariable = "ROYALIDENTITY_CONFIGURATION_TEST_POSTGRES";

    public static string ConnectionString =>
        Environment.GetEnvironmentVariable(ConnectionStringVariable)
        ?? throw new InvalidOperationException(
            $"{ConnectionStringVariable} is not set; this scenario should have been skipped.");

    public static bool IsAvailable =>
        !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(ConnectionStringVariable));
}

/// <summary>Runs only when a PostgreSQL server was published for the suite.</summary>
public sealed class PostgreSqlIntegrationFactAttribute : FactAttribute
{
    public PostgreSqlIntegrationFactAttribute()
    {
        if (!PostgreSqlIntegrationTestEnvironment.IsAvailable)
        {
            Skip = $"Set {PostgreSqlIntegrationTestEnvironment.ConnectionStringVariable} or run " +
                "scripts/Test-ReplayProtectionPostgreSql.ps1 to execute PostgreSQL integration scenarios.";
        }
    }
}
