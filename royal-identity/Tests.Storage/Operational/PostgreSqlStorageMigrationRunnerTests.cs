using Microsoft.EntityFrameworkCore;
using Npgsql;
using RoyalIdentity.Migrations;
using RoyalIdentity.UserAccounts.Infrastructure.Data;
using RoyalIdentity.UserAccounts.Infrastructure.Events;
using RoyalIdentity.UserAccounts.PostgreSql;
using Tests.Storage.Configuration;
using Tests.Storage.Configuration.Support;

namespace Tests.Storage.Operational;

/// <summary>
/// Opt-in acceptance for the external runner across all three PostgreSQL families. Each scenario owns isolated
/// databases so it can run beside the other PostgreSQL suites without sharing schema state.
/// </summary>
public class PostgreSqlStorageMigrationRunnerTests
{
    [StoragePostgreSqlFact]
    [Trait("Category", "PostgreSql")]
    public async Task AllFamilies_SharedDatabase_AreIdempotentAndKeepIndependentHistories()
    {
        await using var database = await PostgreSqlConfigurationDatabase.CreateEmptyAsync();
        var options = new MigrationRunnerOptions
        {
            ConfigurationProvider = ConfigurationDatabaseProvider.PostgreSql,
            ConfigurationConnection = database.ConnectionString,
            OperationalConnection = database.ConnectionString,
            UserAccountsConnection = database.ConnectionString,
            Families = StorageFamilySelection.All,
            DatabaseTopology = StorageDatabaseTopology.Shared,
        };

        Assert.True((await StorageMigrationRunner.RunAsync(options)).Succeeded);
        Assert.True((await StorageMigrationRunner.RunAsync(options)).Succeeded);

        var tables = await TableNamesAsync(database.ConnectionString);
        Assert.Contains("configuration.realms", tables);
        Assert.Contains("operation.protocol_artifacts", tables);
        Assert.Contains("public.UserAccounts", tables);
        Assert.Contains("configuration.__EFMigrationsHistory", tables);
        Assert.Contains("operation.__EFMigrationsHistory", tables);
        Assert.Contains($"public.{UserAccountsDbContext.MigrationsHistoryTableName}", tables);
        Assert.DoesNotContain("public.__EFMigrationsHistory", tables);
    }

    [StoragePostgreSqlFact]
    [Trait("Category", "PostgreSql")]
    public async Task AllFamilies_SharedDatabase_PreserveAndRelocateLegacyUserAccountsHistory()
    {
        await using var database = await PostgreSqlConfigurationDatabase.CreateEmptyAsync();
        await using (var legacy = new UserAccountsPostgreSqlDbContext(
            new DbContextOptionsBuilder<UserAccountsPostgreSqlDbContext>()
                .UseNpgsql(database.ConnectionString)
                .Options,
            new DomainEventDispatcher([])))
        {
            await legacy.Database.MigrateAsync();
        }

        var options = new MigrationRunnerOptions
        {
            ConfigurationProvider = ConfigurationDatabaseProvider.PostgreSql,
            ConfigurationConnection = database.ConnectionString,
            OperationalConnection = database.ConnectionString,
            UserAccountsConnection = database.ConnectionString,
            Families = StorageFamilySelection.All,
            DatabaseTopology = StorageDatabaseTopology.Shared,
        };
        var report = await StorageMigrationRunner.RunAsync(options);

        Assert.True(report.Succeeded);
        Assert.All(report.Families, family => Assert.Equal(StorageMigrationStatus.Applied, family.Status));
        Assert.True((await StorageMigrationRunner.RunAsync(options)).Succeeded);

        var tables = await TableNamesAsync(database.ConnectionString);
        Assert.Contains("configuration.__EFMigrationsHistory", tables);
        Assert.Contains("operation.__EFMigrationsHistory", tables);
        Assert.Contains($"public.{UserAccountsDbContext.MigrationsHistoryTableName}", tables);
        Assert.DoesNotContain("public.__EFMigrationsHistory", tables);
    }

    [StoragePostgreSqlFact]
    [Trait("Category", "PostgreSql")]
    public async Task AllFamilies_SeparateDatabases_MigrateOnlyTheirOwnedSchema()
    {
        await using var configuration = await PostgreSqlConfigurationDatabase.CreateEmptyAsync();
        await using var operational = await PostgreSqlConfigurationDatabase.CreateEmptyAsync();
        await using var userAccounts = await PostgreSqlConfigurationDatabase.CreateEmptyAsync();
        var options = new MigrationRunnerOptions
        {
            ConfigurationProvider = ConfigurationDatabaseProvider.PostgreSql,
            ConfigurationConnection = configuration.ConnectionString,
            OperationalConnection = operational.ConnectionString,
            UserAccountsConnection = userAccounts.ConnectionString,
            Families = StorageFamilySelection.All,
            DatabaseTopology = StorageDatabaseTopology.Separate,
        };

        Assert.True((await StorageMigrationRunner.RunAsync(options)).Succeeded);

        var configurationTables = await TableNamesAsync(configuration.ConnectionString);
        var operationalTables = await TableNamesAsync(operational.ConnectionString);
        var userAccountsTables = await TableNamesAsync(userAccounts.ConnectionString);

        Assert.Contains("configuration.realms", configurationTables);
        Assert.DoesNotContain("operation.protocol_artifacts", configurationTables);
        Assert.DoesNotContain("public.UserAccounts", configurationTables);
        Assert.Contains("operation.protocol_artifacts", operationalTables);
        Assert.DoesNotContain("configuration.realms", operationalTables);
        Assert.DoesNotContain("public.UserAccounts", operationalTables);
        Assert.Contains("public.UserAccounts", userAccountsTables);
        Assert.DoesNotContain("configuration.realms", userAccountsTables);
        Assert.DoesNotContain("operation.protocol_artifacts", userAccountsTables);
    }

    private static async Task<HashSet<string>> TableNamesAsync(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            """
            SELECT table_schema, table_name
            FROM information_schema.tables
            WHERE table_schema NOT IN ('pg_catalog', 'information_schema');
            """,
            connection);
        await using var reader = await command.ExecuteReaderAsync();
        HashSet<string> tables = new(StringComparer.Ordinal);
        while (await reader.ReadAsync())
            tables.Add($"{reader.GetString(0)}.{reader.GetString(1)}");

        return tables;
    }
}
