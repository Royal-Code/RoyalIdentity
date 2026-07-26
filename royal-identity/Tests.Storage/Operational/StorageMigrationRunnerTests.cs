using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using RoyalIdentity.Migrations;
using RoyalIdentity.Storage.EntityFramework.Sqlite;

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
        string? operationalConnection = null)
        => new()
        {
            ConfigurationProvider = ConfigurationDatabaseProvider.Sqlite,
            ConfigurationConnection = configurationConnection,
            Families = families,
            OperationalConnection = operationalConnection,
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

        var tables = await TableNamesAsync(connection);
        Assert.Contains("realms", tables);
        Assert.Contains("__ConfigurationMigrationsHistory", tables);
        Assert.DoesNotContain("protocol_artifacts", tables);
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

        var tables = await TableNamesAsync(connection);
        Assert.Contains("protocol_artifacts", tables);
        Assert.Contains("__OperationalMigrationsHistory", tables);
        Assert.DoesNotContain("realms", tables);
        Assert.DoesNotContain("__ConfigurationMigrationsHistory", tables);
    }

    // One connection for both families is the topology the two histories exist for (DF23).
    [Fact]
    public async Task BothFamilies_OverOneDatabase_KeepSeparateHistories()
    {
        var connection = NewConnectionString();

        var report = await StorageMigrationRunner.RunAsync(Options(connection, StorageFamilySelection.All));

        Assert.True(report.Succeeded);
        Assert.All(report.Families, family => Assert.Equal(StorageMigrationStatus.Applied, family.Status));

        var tables = await TableNamesAsync(connection);
        Assert.Contains("realms", tables);
        Assert.Contains("protocol_artifacts", tables);
        Assert.Contains("__ConfigurationMigrationsHistory", tables);
        Assert.Contains("__OperationalMigrationsHistory", tables);
        Assert.DoesNotContain("__EFMigrationsHistory", tables);
    }

    [Fact]
    public async Task BothFamilies_OverTwoDatabases_MigrateEachInItsOwn()
    {
        var configuration = NewConnectionString();
        var operational = NewConnectionString();

        var report = await StorageMigrationRunner.RunAsync(
            Options(configuration, StorageFamilySelection.All, operational));

        Assert.True(report.Succeeded);

        var configurationTables = await TableNamesAsync(configuration);
        var operationalTables = await TableNamesAsync(operational);

        Assert.Contains("realms", configurationTables);
        Assert.DoesNotContain("protocol_artifacts", configurationTables);
        Assert.Contains("protocol_artifacts", operationalTables);
        Assert.DoesNotContain("realms", operationalTables);
    }

    [Fact]
    public async Task RunningTwice_IsIdempotent()
    {
        var connection = NewConnectionString();

        Assert.True((await StorageMigrationRunner.RunAsync(Options(connection, StorageFamilySelection.All))).Succeeded);
        Assert.True((await StorageMigrationRunner.RunAsync(Options(connection, StorageFamilySelection.All))).Succeeded);

        await using var context = new OperationalSqliteDbContext(
            new DbContextOptionsBuilder<OperationalSqliteDbContext>()
                .UseSqlite(connection, sqlite => sqlite.UseOperationalMigrationsHistory())
                .Options);
        Assert.Empty(await context.Database.GetPendingMigrationsAsync());
    }

    // A failing family is reported on its own, and the next one is not silently applied on top of it.
    [Fact]
    public async Task WhenAFamilyFails_ItIsReportedAlone_AndTheNextIsNotAttempted()
    {
        var report = await StorageMigrationRunner.RunAsync(
            Options("Data Source=/this/path/does/not/exist/royalidentity.db", StorageFamilySelection.All));

        Assert.False(report.Succeeded);
        Assert.Equal(StorageMigrationStatus.Failed, report.For(StorageFamilySelection.Configuration).Status);
        Assert.NotNull(report.For(StorageFamilySelection.Configuration).Failure);
        Assert.Equal(StorageMigrationStatus.NotAttempted, report.For(StorageFamilySelection.Operational).Status);
    }

    // DF19: the seed is Configuration data; asking for it on Operational alone means the command was misread.
    [Fact]
    public void Seed_IsRefusedWhenConfigurationIsNotSelected()
    {
        var failure = Assert.Throws<MigrationRunnerUsageException>(() => MigrationRunnerOptions.Parse(
        [
            "--configuration-provider", "sqlite",
            "--configuration-connection", "Data Source=x.db",
            "--families", "operational",
            "--seed", "product",
            "--key-protector", "plain",
            "--server-admin-redirect-uri", "https://admin.example/callback",
        ]));

        Assert.Contains("Configuration family only", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_AcceptsTheFamilySelectionAndBothConnections()
    {
        var options = MigrationRunnerOptions.Parse(
        [
            "--configuration-provider", "postgresql",
            "--configuration-connection", "Host=c;Database=c",
            "--families", "all",
            "--operational-connection", "Host=o;Database=o",
        ]);

        Assert.Equal(StorageFamilySelection.All, options.Families);
        Assert.Equal("Host=o;Database=o", options.ResolvedOperationalConnection);
        Assert.False(options.SharesOneDatabase);
    }

    // One connection means one database: the Operational family follows the Configuration one.
    [Fact]
    public void Parse_WithoutAnOperationalConnection_SharesTheConfigurationDatabase()
    {
        var options = MigrationRunnerOptions.Parse(
        [
            "--configuration-provider", "sqlite",
            "--configuration-connection", "Data Source=shared.db",
            "--families", "all",
        ]);

        Assert.Equal("Data Source=shared.db", options.ResolvedOperationalConnection);
        Assert.True(options.SharesOneDatabase);
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

    // DF28: a provider error routinely echoes the connection string that produced it, and a run may carry two.
    [Fact]
    public void Diagnostics_RedactBothConnections_AndTheAesKey()
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
                AesKeyEnvironmentVariable = "ROYALIDENTITY_TEST_AES_KEY",
            };

            var sanitized = MigrationRunnerDiagnostics.Sanitize(
                $"failed for '{options.ConfigurationConnection}' and '{options.OperationalConnection}' " +
                "with key c3VwZXItc2VjcmV0LWtleQ==",
                options);

            Assert.DoesNotContain("configuration-secret", sanitized, StringComparison.Ordinal);
            Assert.DoesNotContain("operational-secret", sanitized, StringComparison.Ordinal);
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
