using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using RoyalIdentity.Migrations;
using RoyalIdentity.Storage.EntityFramework.Sqlite;
using RoyalIdentity.UserAccounts.Infrastructure.Data;
using RoyalIdentity.UserAccounts.Infrastructure.Events;
using RoyalIdentity.UserAccounts.Sqlite;

namespace Tests.Storage.Operational;

/// <summary>
/// The runner across families (plan Fase 7, DF23). It is the only executable that applies migrations, and it
/// applies each family as its own sequence: Configuration-only, Operational-only, both over one database and
/// both over two. The report is per family on purpose — there is no joint atomicity to claim, and a failure in
/// one says nothing about the other beyond what its own result states.
/// </summary>
public class StorageMigrationRunnerTests : IDisposable
{
    private readonly List<string> files = [];

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        foreach (var file in files)
        {
            try
            {
                File.Delete(file);
            }
            catch (IOException)
            {
                // A leftover temp file is not worth failing a green run over.
            }
        }

        GC.SuppressFinalize(this);
    }

    private string NewDatabaseFile()
    {
        var path = Path.Combine(Path.GetTempPath(), $"royalidentity-runner-{Guid.NewGuid():N}.db");
        files.Add(path);

        return path;
    }

    private string NewConnectionString() => $"Data Source={NewDatabaseFile()}";

    private static MigrationRunnerOptions Options(
        string configurationConnection,
        StorageFamilySelection families,
        string? operationalConnection = null,
        StorageDatabaseTopology? databaseTopology = null,
        string? userAccountsConnection = null)
        => new()
        {
            ConfigurationProvider = ConfigurationDatabaseProvider.Sqlite,
            ConfigurationConnection = families.HasFlag(StorageFamilySelection.Configuration)
                ? configurationConnection
                : null,
            Families = families,
            OperationalConnection = families.HasFlag(StorageFamilySelection.Operational)
                ? operationalConnection ?? configurationConnection
                : null,
            UserAccountsConnection = families.HasFlag(StorageFamilySelection.UserAccounts)
                ? userAccountsConnection ?? configurationConnection
                : null,
            DatabaseTopology = databaseTopology,
        };

    private static async Task<List<string>> TableNamesAsync(string connectionString)
    {
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT name FROM sqlite_master WHERE type = 'table' AND name NOT LIKE 'sqlite_%' ORDER BY name;";

        var tables = new List<string>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            tables.Add(reader.GetString(0));

        return tables;
    }

    // Configuration-only leaves no Operational trace: selecting one family never implies the other.
    [Fact]
    public async Task ConfigurationOnly_MigratesConfiguration_AndLeavesOperationalAbsent()
    {
        var connection = NewConnectionString();

        var report = await StorageMigrationRunner.RunAsync(
            Options(connection, StorageFamilySelection.Configuration));

        Assert.True(report.Succeeded);
        Assert.Equal(StorageMigrationStatus.Applied, report.For(StorageFamilySelection.Configuration).Status);
        Assert.Equal(StorageMigrationStatus.Skipped, report.For(StorageFamilySelection.Operational).Status);
        Assert.Equal(StorageMigrationStatus.Skipped, report.For(StorageFamilySelection.UserAccounts).Status);

        var tables = await TableNamesAsync(connection);
        Assert.Contains("realms", tables);
        Assert.Contains("__ConfigurationMigrationsHistory", tables);
        Assert.DoesNotContain("protocol_artifacts", tables);
        Assert.DoesNotContain("UserAccounts", tables);
        Assert.DoesNotContain("__OperationalMigrationsHistory", tables);
    }

    [Fact]
    public async Task OperationalOnly_MigratesOperational_AndLeavesConfigurationAbsent()
    {
        var connection = NewConnectionString();

        var report = await StorageMigrationRunner.RunAsync(
            Options(connection, StorageFamilySelection.Operational));

        Assert.True(report.Succeeded);
        Assert.Equal(StorageMigrationStatus.Skipped, report.For(StorageFamilySelection.Configuration).Status);
        Assert.Equal(StorageMigrationStatus.Applied, report.For(StorageFamilySelection.Operational).Status);
        Assert.Equal(StorageMigrationStatus.Skipped, report.For(StorageFamilySelection.UserAccounts).Status);

        var tables = await TableNamesAsync(connection);
        Assert.Contains("protocol_artifacts", tables);
        Assert.Contains("__OperationalMigrationsHistory", tables);
        Assert.DoesNotContain("realms", tables);
        Assert.DoesNotContain("UserAccounts", tables);
        Assert.DoesNotContain("__ConfigurationMigrationsHistory", tables);
    }

    [Fact]
    public async Task UserAccountsOnly_MigratesUserAccounts_AndLeavesIdpFamiliesAbsent()
    {
        var connection = NewConnectionString();

        var report = await StorageMigrationRunner.RunAsync(
            Options(connection, StorageFamilySelection.UserAccounts));

        Assert.True(report.Succeeded);
        Assert.Equal(StorageMigrationStatus.Skipped, report.For(StorageFamilySelection.Configuration).Status);
        Assert.Equal(StorageMigrationStatus.Skipped, report.For(StorageFamilySelection.Operational).Status);
        Assert.Equal(StorageMigrationStatus.Applied, report.For(StorageFamilySelection.UserAccounts).Status);

        var tables = await TableNamesAsync(connection);
        Assert.Contains("UserAccounts", tables);
        Assert.Contains(UserAccountsDbContext.MigrationsHistoryTableName, tables);
        Assert.DoesNotContain("realms", tables);
        Assert.DoesNotContain("protocol_artifacts", tables);
    }

    [Fact]
    public async Task UserAccountsOnly_RelocatesItsLegacyDefaultHistory()
    {
        var connection = NewConnectionString();
        await using (var legacy = new UserAccountsSqliteDbContext(
            new DbContextOptionsBuilder<UserAccountsSqliteDbContext>()
                .UseSqlite(connection)
                .Options,
            new DomainEventDispatcher([])))
        {
            await legacy.Database.MigrateAsync();
        }

        var report = await StorageMigrationRunner.RunAsync(
            Options(connection, StorageFamilySelection.UserAccounts));

        Assert.True(report.Succeeded);
        var tables = await TableNamesAsync(connection);
        Assert.Contains(UserAccountsDbContext.MigrationsHistoryTableName, tables);
        Assert.DoesNotContain("__EFMigrationsHistory", tables);
    }

    // One database for all families is the topology their independent histories must make safe.
    [Fact]
    public async Task AllFamilies_OverOneDatabase_KeepSeparateHistories()
    {
        var connection = NewConnectionString();

        var report = await StorageMigrationRunner.RunAsync(Options(
            connection,
            StorageFamilySelection.All,
            databaseTopology: StorageDatabaseTopology.Shared));

        Assert.True(report.Succeeded);
        Assert.All(report.Families, family => Assert.Equal(StorageMigrationStatus.Applied, family.Status));

        var tables = await TableNamesAsync(connection);
        Assert.Contains("realms", tables);
        Assert.Contains("protocol_artifacts", tables);
        Assert.Contains("UserAccounts", tables);
        Assert.Contains("__ConfigurationMigrationsHistory", tables);
        Assert.Contains("__OperationalMigrationsHistory", tables);
        Assert.Contains(UserAccountsDbContext.MigrationsHistoryTableName, tables);
        Assert.DoesNotContain("__EFMigrationsHistory", tables);
    }

    [Fact]
    public async Task AllFamilies_OverThreeDatabases_MigrateEachInItsOwn()
    {
        var configuration = NewConnectionString();
        var operational = NewConnectionString();
        var userAccounts = NewConnectionString();

        var report = await StorageMigrationRunner.RunAsync(
            Options(
                configuration,
                StorageFamilySelection.All,
                operational,
                userAccountsConnection: userAccounts));

        Assert.True(report.Succeeded);

        var configurationTables = await TableNamesAsync(configuration);
        var operationalTables = await TableNamesAsync(operational);
        var userAccountsTables = await TableNamesAsync(userAccounts);

        Assert.Contains("realms", configurationTables);
        Assert.DoesNotContain("protocol_artifacts", configurationTables);
        Assert.DoesNotContain("UserAccounts", configurationTables);
        Assert.Contains("protocol_artifacts", operationalTables);
        Assert.DoesNotContain("realms", operationalTables);
        Assert.DoesNotContain("UserAccounts", operationalTables);
        Assert.Contains("UserAccounts", userAccountsTables);
        Assert.DoesNotContain("realms", userAccountsTables);
        Assert.DoesNotContain("protocol_artifacts", userAccountsTables);
    }

    [Fact]
    public async Task RunningTwice_IsIdempotent()
    {
        var connection = NewConnectionString();

        var options = Options(
            connection,
            StorageFamilySelection.All,
            databaseTopology: StorageDatabaseTopology.Shared);

        Assert.True((await StorageMigrationRunner.RunAsync(options)).Succeeded);
        Assert.True((await StorageMigrationRunner.RunAsync(options)).Succeeded);

        await using var context = new OperationalSqliteDbContext(
            new DbContextOptionsBuilder<OperationalSqliteDbContext>()
                .UseSqlite(connection, sqlite => sqlite.UseOperationalMigrationsHistory())
                .Options);
        Assert.Empty(await context.Database.GetPendingMigrationsAsync());
    }

    [Fact]
    public async Task RunningTwice_WithDemoSeed_IsIdempotentAcrossAllFamilies()
    {
        var connection = NewConnectionString();
        var options = new MigrationRunnerOptions
        {
            ConfigurationProvider = ConfigurationDatabaseProvider.Sqlite,
            ConfigurationConnection = connection,
            OperationalConnection = connection,
            UserAccountsConnection = connection,
            Families = StorageFamilySelection.All,
            DatabaseTopology = StorageDatabaseTopology.Shared,
            Seed = ConfigurationSeedMode.Demo,
            KeyProtector = ConfigurationKeyProtector.Plain,
        };

        Assert.True((await StorageMigrationRunner.RunAsync(options)).Succeeded);
        Assert.True((await StorageMigrationRunner.RunAsync(options)).Succeeded);

        await using var database = new SqliteConnection(connection);
        await database.OpenAsync();
        await using var command = database.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM realms WHERE id = 'demo_realm';";
        Assert.Equal(1L, Convert.ToInt64(await command.ExecuteScalarAsync()));
    }

    // Over ONE database a failure stops the run: migrating Operational on top of a Configuration that just
    // failed would migrate a database in an unknown state.
    [Fact]
    public async Task WhenAFamilyFails_OverOneDatabase_TheNextIsNotAttempted()
    {
        const string unreachable = "Data Source=/this/path/does/not/exist/royalidentity.db";

        var report = await StorageMigrationRunner.RunAsync(Options(
            unreachable,
            StorageFamilySelection.All,
            databaseTopology: StorageDatabaseTopology.Shared));

        Assert.False(report.Succeeded);
        Assert.Equal(StorageMigrationStatus.Failed, report.For(StorageFamilySelection.Configuration).Status);
        Assert.NotNull(report.For(StorageFamilySelection.Configuration).Failure);
        Assert.Equal(StorageMigrationStatus.NotAttempted, report.For(StorageFamilySelection.Operational).Status);
        Assert.Equal(StorageMigrationStatus.NotAttempted, report.For(StorageFamilySelection.UserAccounts).Status);
    }

    // Two different connection strings can still reach one physical database, for example through distinct
    // Configuration and Operational credentials. Explicit topology, not textual equality, controls fail-stop.
    [Fact]
    public async Task WhenSharedTopologyUsesDifferentConnectionStrings_AFailureStillStopsTheRun()
    {
        const string unreachableConfiguration =
            "Data Source=/this/path/does/not/exist/royalidentity.db;Default Timeout=1";
        const string differentlyFormattedSameTarget =
            "Default Timeout=2;Data Source=/this/path/does/not/exist/royalidentity.db";

        var report = await StorageMigrationRunner.RunAsync(Options(
            unreachableConfiguration,
            StorageFamilySelection.All,
            differentlyFormattedSameTarget,
            StorageDatabaseTopology.Shared,
            differentlyFormattedSameTarget));

        Assert.False(report.Succeeded);
        Assert.Equal(StorageMigrationStatus.Failed, report.For(StorageFamilySelection.Configuration).Status);
        Assert.Equal(StorageMigrationStatus.NotAttempted, report.For(StorageFamilySelection.Operational).Status);
        Assert.Equal(StorageMigrationStatus.NotAttempted, report.For(StorageFamilySelection.UserAccounts).Status);
    }

    // Over TWO databases it does not: coupling them would be exactly the joint atomicity that does not exist,
    // and would hide a perfectly healthy Operational database behind an unrelated failure.
    [Fact]
    public async Task WhenAFamilyFails_OverTwoDatabases_TheOtherIsStillAttempted()
    {
        const string unreachable = "Data Source=/this/path/does/not/exist/royalidentity.db";
        var operational = NewConnectionString();
        var userAccounts = NewConnectionString();

        var report = await StorageMigrationRunner.RunAsync(
            Options(
                unreachable,
                StorageFamilySelection.All,
                operational,
                userAccountsConnection: userAccounts));

        Assert.False(report.Succeeded);
        Assert.Equal(StorageMigrationStatus.Failed, report.For(StorageFamilySelection.Configuration).Status);
        // The Operational database is untouched by the Configuration failure, and says so.
        Assert.Equal(StorageMigrationStatus.Applied, report.For(StorageFamilySelection.Operational).Status);
        Assert.Equal(StorageMigrationStatus.Applied, report.For(StorageFamilySelection.UserAccounts).Status);
        Assert.Contains("protocol_artifacts", await TableNamesAsync(operational));
        Assert.Contains("UserAccounts", await TableNamesAsync(userAccounts));
    }

    // And the same holds the other way round: an Operational failure never invalidates a Configuration that
    // already applied to its own database.
    [Fact]
    public async Task WhenTheSecondFamilyFails_OverTwoDatabases_TheFirstIsStillReportedApplied()
    {
        var configuration = NewConnectionString();
        var userAccounts = NewConnectionString();

        var report = await StorageMigrationRunner.RunAsync(Options(
            configuration,
            StorageFamilySelection.All,
            "Data Source=/this/path/does/not/exist/royalidentity.db",
            userAccountsConnection: userAccounts));

        Assert.False(report.Succeeded);
        Assert.Equal(StorageMigrationStatus.Applied, report.For(StorageFamilySelection.Configuration).Status);
        Assert.Equal(StorageMigrationStatus.Failed, report.For(StorageFamilySelection.Operational).Status);
        Assert.Equal(StorageMigrationStatus.Applied, report.For(StorageFamilySelection.UserAccounts).Status);
        Assert.Contains("realms", await TableNamesAsync(configuration));
        Assert.Contains("UserAccounts", await TableNamesAsync(userAccounts));
    }

    [Fact]
    public async Task WhenTheThirdFamilyFails_TheEarlierFamiliesRemainReportedApplied()
    {
        var configuration = NewConnectionString();
        var operational = NewConnectionString();

        var report = await StorageMigrationRunner.RunAsync(Options(
            configuration,
            StorageFamilySelection.All,
            operational,
            userAccountsConnection: "Data Source=/this/path/does/not/exist/royalidentity.db"));

        Assert.False(report.Succeeded);
        Assert.Equal(StorageMigrationStatus.Applied, report.For(StorageFamilySelection.Configuration).Status);
        Assert.Equal(StorageMigrationStatus.Applied, report.For(StorageFamilySelection.Operational).Status);
        Assert.Equal(StorageMigrationStatus.Failed, report.For(StorageFamilySelection.UserAccounts).Status);
        Assert.Contains("realms", await TableNamesAsync(configuration));
        Assert.Contains("protocol_artifacts", await TableNamesAsync(operational));
    }

    // DF19: the seed is Configuration data; asking for it on Operational alone means the command was misread.
    [Fact]
    public void Seed_IsRefusedWhenConfigurationIsNotSelected()
    {
        var failure = Assert.Throws<MigrationRunnerUsageException>(() => MigrationRunnerOptions.Parse(
        [
            "--configuration-provider", "sqlite",
            "--families", "operational",
            "--operational-connection", "Data Source=x.db",
            "--seed", "product",
            "--key-protector", "plain",
            "--server-admin-redirect-uri", "https://admin.example/callback",
        ]));

        Assert.Contains("Configuration family only", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_AcceptsAllFamiliesAndTheirIndependentConnections()
    {
        var options = MigrationRunnerOptions.Parse(
        [
            "--configuration-provider", "postgresql",
            "--configuration-connection", "Host=c;Database=c",
            "--families", "all",
            "--operational-connection", "Host=o;Database=o",
            "--user-accounts-connection", "Host=u;Database=u",
        ]);

        Assert.Equal(StorageFamilySelection.All, options.Families);
        Assert.Equal("Host=o;Database=o", options.ResolvedOperationalConnection);
        Assert.Equal("Host=u;Database=u", options.ResolvedUserAccountsConnection);
        Assert.Equal(StorageDatabaseTopology.Separate, options.ResolvedDatabaseTopology);
        Assert.False(options.SharesOneDatabase);
    }

    [Fact]
    public void Parse_RequiresAnExplicitConnectionForEverySelectedFamily()
    {
        var failure = Assert.Throws<MigrationRunnerUsageException>(() => MigrationRunnerOptions.Parse(
        [
            "--configuration-provider", "sqlite",
            "--configuration-connection", "Data Source=shared.db",
            "--families", "all",
            "--operational-connection", "Data Source=shared.db",
        ]));

        Assert.Contains("--user-accounts-connection", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_ExplicitSharedTopology_AllowsDifferentConnectionsForOneDatabase()
    {
        var options = MigrationRunnerOptions.Parse(
        [
            "--configuration-provider", "postgresql",
            "--configuration-connection", "Host=db;Database=identity;Username=configuration",
            "--families", "all",
            "--operational-connection", "Host=db;Database=identity;Username=operation",
            "--user-accounts-connection", "Host=db;Database=identity;Username=users",
            "--database-topology", "shared",
        ]);

        Assert.Equal(StorageDatabaseTopology.Shared, options.ResolvedDatabaseTopology);
        Assert.True(options.SharesOneDatabase);
    }

    [Fact]
    public void Parse_RejectsASelectedFamilyWithoutItsConnection()
    {
        var failure = Assert.Throws<MigrationRunnerUsageException>(() => MigrationRunnerOptions.Parse(
        [
            "--configuration-provider", "postgresql",
            "--configuration-connection", "Host=db;Database=configuration",
            "--families", "all",
            "--database-topology", "separate",
        ]));

        Assert.Contains("--operational-connection", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Runner_RejectsASelectedFamilyWithoutItsConnection_InDirectOptions()
    {
        var options = new MigrationRunnerOptions
        {
            ConfigurationProvider = ConfigurationDatabaseProvider.Sqlite,
            ConfigurationConnection = NewConnectionString(),
            Families = StorageFamilySelection.All,
            UserAccountsConnection = NewConnectionString(),
            DatabaseTopology = StorageDatabaseTopology.Separate,
        };

        var failure = await Assert.ThrowsAsync<MigrationRunnerUsageException>(
            () => StorageMigrationRunner.RunAsync(options));

        Assert.Contains("Operational family requires", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_RejectsTopologyWhenMultipleFamiliesAreNotSelected()
    {
        var failure = Assert.Throws<MigrationRunnerUsageException>(() => MigrationRunnerOptions.Parse(
        [
            "--configuration-provider", "sqlite",
            "--configuration-connection", "Data Source=configuration.db",
            "--database-topology", "shared",
        ]));

        Assert.Contains("multiple storage families", failure.Message, StringComparison.Ordinal);
    }

    // An Operational connection without the Operational family would silently do nothing.
    [Fact]
    public void Parse_RejectsAnOperationalConnectionWithoutTheFamily()
    {
        var failure = Assert.Throws<MigrationRunnerUsageException>(() => MigrationRunnerOptions.Parse(
        [
            "--configuration-provider", "sqlite",
            "--configuration-connection", "Data Source=c.db",
            "--operational-connection", "Data Source=o.db",
        ]));

        Assert.Contains("Operational family was not selected", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_KeepsProviderAndSeedIndependent()
    {
        var sqliteProduct = MigrationRunnerOptions.Parse(
        [
            "--configuration-provider", "sqlite",
            "--configuration-connection", "Data Source=product.db",
            "--seed", "product",
            "--key-protector", "plain",
            "--server-admin-redirect-uri", "https://admin.example/callback",
        ]);
        var postgreSqlDemo = MigrationRunnerOptions.Parse(
        [
            "--configuration-provider", "postgresql",
            "--configuration-connection", "Host=db;Database=demo",
            "--seed", "demo",
            "--key-protector", "plain",
        ]);

        Assert.Equal(ConfigurationSeedMode.Product, sqliteProduct.Seed);
        Assert.Equal(ConfigurationDatabaseProvider.Sqlite, sqliteProduct.ConfigurationProvider);
        Assert.Equal(ConfigurationSeedMode.Demo, postgreSqlDemo.Seed);
        Assert.Equal(ConfigurationDatabaseProvider.PostgreSql, postgreSqlDemo.ConfigurationProvider);
    }

    [Fact]
    public void Parse_AcceptsAnExplicitSubsetOfFamilies()
    {
        var options = MigrationRunnerOptions.Parse(
        [
            "--configuration-provider", "sqlite",
            "--families", "configuration,user-accounts",
            "--configuration-connection", "Data Source=configuration.db",
            "--user-accounts-connection", "Data Source=users.db",
        ]);

        Assert.True(options.Families.HasFlag(StorageFamilySelection.Configuration));
        Assert.False(options.Families.HasFlag(StorageFamilySelection.Operational));
        Assert.True(options.Families.HasFlag(StorageFamilySelection.UserAccounts));
    }

    [Fact]
    public void Parse_AcceptsTheProviderWideOption_AndRejectsTwoProviderSelectors()
    {
        var options = MigrationRunnerOptions.Parse(
        [
            "--provider", "sqlite",
            "--families", "user-accounts",
            "--user-accounts-connection", "Data Source=users.db",
        ]);

        Assert.Equal(ConfigurationDatabaseProvider.Sqlite, options.ConfigurationProvider);
        Assert.Throws<MigrationRunnerUsageException>(() => MigrationRunnerOptions.Parse(
        [
            "--provider", "sqlite",
            "--configuration-provider", "sqlite",
            "--configuration-connection", "Data Source=configuration.db",
        ]));
    }

    [Fact]
    public void Parse_DoesNotExposeFamilySpecificProviderSelectors()
        => Assert.Throws<MigrationRunnerUsageException>(() => MigrationRunnerOptions.Parse(
        [
            "--configuration-provider", "sqlite",
            "--families", "user-accounts",
            "--user-accounts-connection", "Data Source=users.db",
            "--user-accounts-provider", "postgresql",
        ]));

    // DF28: a provider error routinely echoes the connection string that produced it, and a run may carry three.
    [Fact]
    public void Diagnostics_RedactAllConnections_AndTheAesKey()
    {
        Environment.SetEnvironmentVariable("ROYALIDENTITY_TEST_AES_KEY", "c3VwZXItc2VjcmV0LWtleQ==");
        try
        {
            var options = new MigrationRunnerOptions
            {
                ConfigurationProvider = ConfigurationDatabaseProvider.PostgreSql,
                ConfigurationConnection = "Host=c;Database=c;Username=u;Password=configuration-secret",
                Families = StorageFamilySelection.All,
                OperationalConnection = "Host=o;Database=o;Username=u;Password=operational-secret",
                UserAccountsConnection = "Host=a;Database=a;Username=u;Password=accounts-secret",
                AesKeyEnvironmentVariable = "ROYALIDENTITY_TEST_AES_KEY",
            };

            var sanitized = MigrationRunnerDiagnostics.Sanitize(
                $"failed for '{options.ConfigurationConnection}', '{options.OperationalConnection}' and " +
                $"'{options.UserAccountsConnection}' " +
                "with key c3VwZXItc2VjcmV0LWtleQ==",
                options);

            Assert.DoesNotContain("configuration-secret", sanitized, StringComparison.Ordinal);
            Assert.DoesNotContain("operational-secret", sanitized, StringComparison.Ordinal);
            Assert.DoesNotContain("accounts-secret", sanitized, StringComparison.Ordinal);
            Assert.DoesNotContain("c3VwZXItc2VjcmV0LWtleQ==", sanitized, StringComparison.Ordinal);
            Assert.Contains("[REDACTED CONNECTION]", sanitized, StringComparison.Ordinal);
        }
        finally
        {
            Environment.SetEnvironmentVariable("ROYALIDENTITY_TEST_AES_KEY", null);
        }
    }

    [Fact]
    public void Parse_RejectsAnUnknownFamilySelection()
        => Assert.Throws<MigrationRunnerUsageException>(() => MigrationRunnerOptions.Parse(
        [
            "--configuration-provider", "sqlite",
            "--configuration-connection", "Data Source=c.db",
            "--families", "everything",
        ]));
}
