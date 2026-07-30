using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using RoyalIdentity.Storage.EntityFramework.Migrations;
using RoyalIdentity.Storage.EntityFramework.Sqlite;
using Tests.Storage.Operational.Support;

namespace Tests.Storage.Operational;

/// <summary>
/// Proves the checked-in SQLite Operational migration and the migrations history topology of plan DF23 over a
/// real database: each family records itself in its own history, the two coexist in one file, a database
/// migrated by Plano 2 is relocated off EF's default history before anything else, and the versioned SQL
/// creates the same schema without running a host.
/// </summary>
public class SqliteOperationalMigrationTests
{
    private static readonly string[] ExpectedOperationalTables =
    [
        "authorize_parameters",
        "consents",
        "protocol_artifacts",
        "replay_handles",
        "user_session_clients",
        "user_sessions",
    ];

    private static async Task<List<string>> TableNamesAsync(SqliteConnection connection)
    {
        var tables = new List<string>();
        await using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT name FROM sqlite_master WHERE type = 'table' AND name NOT LIKE 'sqlite_%' ORDER BY name;";
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            tables.Add(reader.GetString(0));

        return tables;
    }

    /// <summary>EF's own bookkeeping tables, by the underscore prefix EF gives them.</summary>
    private static async Task<List<string>> HistoryTableNamesAsync(SqliteConnection connection)
        => [.. (await TableNamesAsync(connection)).Where(name => name.StartsWith("__", StringComparison.Ordinal))];

    [Fact]
    public async Task Migrate_RecordsInitialOperationalInItsOwnHistory()
    {
        await using var database = await SqliteOperationalDatabase.CreateMigratedAsync();
        await using var context = database.NewOperationalContext();

        var applied = await context.Database.GetAppliedMigrationsAsync();

        Assert.Contains(applied, id => id.EndsWith("_InitialOperational", StringComparison.Ordinal));
        Assert.Empty(await context.Database.GetPendingMigrationsAsync());
    }

    // DF23: sharing one file is the case that forces the split. Each family must see only its own applied
    // migrations, and both history tables must exist side by side.
    [Fact]
    public async Task BothFamilies_InTheSameDatabase_KeepSeparateHistories()
    {
        await using var database = await SqliteOperationalDatabase.CreateMigratedAsync();

        var histories = await HistoryTableNamesAsync(database.Connection);

        Assert.Contains("__ConfigurationMigrationsHistory", histories);
        Assert.Contains("__OperationalMigrationsHistory", histories);
        Assert.DoesNotContain("__EFMigrationsHistory", histories);

        await using var configuration = database.NewConfigurationContext();
        await using var operational = database.NewOperationalContext();

        var configurationApplied = await configuration.Database.GetAppliedMigrationsAsync();
        var operationalApplied = await operational.Database.GetAppliedMigrationsAsync();

        // Each family sees only its own evolution line: no Operational id leaks into the Configuration history
        // or the other way round, which is the whole point of the split.
        Assert.Contains(configurationApplied, id => id.EndsWith("_InitialConfiguration", StringComparison.Ordinal));
        Assert.Contains(operationalApplied, id => id.EndsWith("_InitialOperational", StringComparison.Ordinal));

        // Stated as set equality rather than as a name suffix, so it keeps meaning what it says as each family
        // gains migrations: each history holds exactly its own family's evolution line, and the two never share
        // an id.
        Assert.Empty(configurationApplied.Intersect(operationalApplied, StringComparer.Ordinal));
        Assert.Equal(
            configuration.Database.GetMigrations().Order(StringComparer.Ordinal),
            configurationApplied.Order(StringComparer.Ordinal));
        Assert.Equal(
            operational.Database.GetMigrations().Order(StringComparer.Ordinal),
            operationalApplied.Order(StringComparer.Ordinal));
        Assert.Empty(await configuration.Database.GetPendingMigrationsAsync());
        Assert.Empty(await operational.Database.GetPendingMigrationsAsync());
    }

    [Fact]
    public async Task Migrate_CreatesTheOperationalTables()
    {
        await using var database = await SqliteOperationalDatabase.CreateMigratedAsync();

        var tables = await TableNamesAsync(database.Connection);

        Assert.All(ExpectedOperationalTables, table => Assert.Contains(table, tables));
    }

    // The bootstrap must run before EF consults the new history; its observable states are pinned here.
    [Fact]
    public async Task Bootstrap_OnAnEmptyDatabase_DoesNothing()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var outcome = await new SqliteMigrationsHistoryBootstrap().RunAsync(connection);

        Assert.Equal(MigrationsHistoryBootstrapOutcome.NoHistory, outcome);
    }

    [Fact]
    public async Task Bootstrap_WithOnlyForeignLegacyHistory_LeavesItForItsOwner()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await CreateHistoryAsync(connection, "__EFMigrationsHistory", "20260721010154_InitialCreate");

        var outcome = await new SqliteMigrationsHistoryBootstrap().RunAsync(connection);

        Assert.Equal(MigrationsHistoryBootstrapOutcome.ForeignHistory, outcome);
        Assert.Equal(
            ["20260721010154_InitialCreate"],
            await MigrationIdsAsync(connection, "__EFMigrationsHistory"));
        Assert.DoesNotContain("__ConfigurationMigrationsHistory", await HistoryTableNamesAsync(connection));
    }

    [Fact]
    public async Task Bootstrap_WithConfigurationAndForeignLegacyHistories_LeavesBothUntouched()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await CreateHistoryAsync(connection, "__EFMigrationsHistory", "20260721010154_InitialCreate");
        await CreateHistoryAsync(
            connection,
            "__ConfigurationMigrationsHistory",
            "20260722164339_InitialConfiguration");

        var outcome = await new SqliteMigrationsHistoryBootstrap().RunAsync(connection);

        Assert.Equal(MigrationsHistoryBootstrapOutcome.AlreadyRelocated, outcome);
        Assert.Equal(
            ["20260721010154_InitialCreate"],
            await MigrationIdsAsync(connection, "__EFMigrationsHistory"));
        Assert.Equal(
            ["20260722164339_InitialConfiguration"],
            await MigrationIdsAsync(connection, "__ConfigurationMigrationsHistory"));
    }

    [Fact]
    public async Task Bootstrap_WithOnlyTheLegacyHistory_RelocatesItPreservingTheIds()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await CreateHistoryAsync(connection, "__EFMigrationsHistory", "20260722164339_InitialConfiguration");

        var outcome = await new SqliteMigrationsHistoryBootstrap().RunAsync(connection);

        Assert.Equal(MigrationsHistoryBootstrapOutcome.Relocated, outcome);
        Assert.Equal(
            ["20260722164339_InitialConfiguration"],
            await MigrationIdsAsync(connection, "__ConfigurationMigrationsHistory"));
        Assert.DoesNotContain("__EFMigrationsHistory", await HistoryTableNamesAsync(connection));
    }

    [Fact]
    public async Task Bootstrap_WithOnlyTheNewHistory_IsANoOp()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await CreateHistoryAsync(connection, "__ConfigurationMigrationsHistory", "20260722164339_InitialConfiguration");

        var outcome = await new SqliteMigrationsHistoryBootstrap().RunAsync(connection);

        Assert.Equal(MigrationsHistoryBootstrapOutcome.AlreadyRelocated, outcome);
        Assert.Equal(
            ["20260722164339_InitialConfiguration"],
            await MigrationIdsAsync(connection, "__ConfigurationMigrationsHistory"));
    }

    // Ambiguity fails closed: merging or dropping either table would silently reapply or skip migrations.
    [Fact]
    public async Task Bootstrap_WithBothHistories_FailsClosedWithoutTouchingEither()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await CreateHistoryAsync(connection, "__EFMigrationsHistory", "20260722164339_InitialConfiguration");
        await CreateHistoryAsync(connection, "__ConfigurationMigrationsHistory", "20260101000000_Other");

        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await new SqliteMigrationsHistoryBootstrap().RunAsync(connection));

        Assert.Equal(
            ["20260722164339_InitialConfiguration"],
            await MigrationIdsAsync(connection, "__EFMigrationsHistory"));
        Assert.Equal(["20260101000000_Other"], await MigrationIdsAsync(connection, "__ConfigurationMigrationsHistory"));
    }

    [Fact]
    public async Task Bootstrap_WithMixedLegacyHistory_FailsClosedWithoutTouchingIt()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await CreateHistoryAsync(connection, "__EFMigrationsHistory", "20260722164339_InitialConfiguration");
        await AddHistoryIdAsync(connection, "__EFMigrationsHistory", "20260721010154_InitialCreate");

        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await new SqliteMigrationsHistoryBootstrap().RunAsync(connection));

        Assert.Equal(
            ["20260721010154_InitialCreate", "20260722164339_InitialConfiguration"],
            await MigrationIdsAsync(connection, "__EFMigrationsHistory"));
        Assert.DoesNotContain("__ConfigurationMigrationsHistory", await HistoryTableNamesAsync(connection));
    }

    [Fact]
    public async Task Bootstrap_RepeatedAfterRelocating_IsIdempotent()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await CreateHistoryAsync(connection, "__EFMigrationsHistory", "20260722164339_InitialConfiguration");
        var bootstrap = new SqliteMigrationsHistoryBootstrap();

        Assert.Equal(MigrationsHistoryBootstrapOutcome.Relocated, await bootstrap.RunAsync(connection));
        Assert.Equal(MigrationsHistoryBootstrapOutcome.AlreadyRelocated, await bootstrap.RunAsync(connection));
        Assert.Equal(MigrationsHistoryBootstrapOutcome.AlreadyRelocated, await bootstrap.RunAsync(connection));

        Assert.Equal(
            ["20260722164339_InitialConfiguration"],
            await MigrationIdsAsync(connection, "__ConfigurationMigrationsHistory"));
    }

    // After the bootstrap, EF sees the database as already migrated instead of trying to recreate it.
    [Fact]
    public async Task Bootstrap_ThenMigrate_LeavesNoPendingMigration()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        // A database as Plano 2 left it: schema applied under EF's default history name.
        await using (var legacy = new ConfigurationSqliteDbContext(
            new DbContextOptionsBuilder<ConfigurationSqliteDbContext>().UseSqlite(connection).Options))
        {
            await legacy.Database.MigrateAsync();
        }

        Assert.Equal(
            MigrationsHistoryBootstrapOutcome.Relocated,
            await new SqliteMigrationsHistoryBootstrap().RunAsync(connection));

        await using var migrated = new ConfigurationSqliteDbContext(
            new DbContextOptionsBuilder<ConfigurationSqliteDbContext>()
                .UseSqlite(connection, sqlite => sqlite.UseConfigurationMigrationsHistory())
                .Options);

        Assert.Empty(await migrated.Database.GetPendingMigrationsAsync());
        Assert.Contains(
            await migrated.Database.GetAppliedMigrationsAsync(),
            id => id.EndsWith("_InitialConfiguration", StringComparison.Ordinal));
    }

    [Fact]
    public async Task GeneratedSqlScript_ExecutesAgainstEmptyDatabase()
    {
        // The scripts are an evolution line, not a snapshot: applying them in order is what a reviewer running
        // them against a database would do, and it is the only way the sequence stays verified as it grows.
        string[] scripts =
        [
            "0001_initial_operational.sql",
            "0002_add_replay_handles.sql",
        ];

        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        foreach (var name in scripts)
        {
            var scriptPath = Path.Combine(
                FindRepositoryRoot(), "scripts", "sql", "operational", "sqlite", name);
            await using var command = connection.CreateCommand();
            command.CommandText = await File.ReadAllTextAsync(scriptPath);
            await command.ExecuteNonQueryAsync();
        }

        var tables = await TableNamesAsync(connection);
        Assert.All(ExpectedOperationalTables, table => Assert.Contains(table, tables));
        Assert.Contains("__OperationalMigrationsHistory", tables);
    }

    private static async Task CreateHistoryAsync(SqliteConnection connection, string table, string migrationId)
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            $"CREATE TABLE \"{table}\" (\"MigrationId\" TEXT NOT NULL CONSTRAINT \"PK_{table}\" PRIMARY KEY, " +
            "\"ProductVersion\" TEXT NOT NULL); " +
            $"INSERT INTO \"{table}\" (\"MigrationId\", \"ProductVersion\") VALUES ($id, '10.0.10');";
        command.Parameters.AddWithValue("$id", migrationId);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task AddHistoryIdAsync(SqliteConnection connection, string table, string migrationId)
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            $"INSERT INTO \"{table}\" (\"MigrationId\", \"ProductVersion\") VALUES ($id, '10.0.10');";
        command.Parameters.AddWithValue("$id", migrationId);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<List<string>> MigrationIdsAsync(SqliteConnection connection, string table)
    {
        var ids = new List<string>();
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT \"MigrationId\" FROM \"{table}\" ORDER BY \"MigrationId\";";
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            ids.Add(reader.GetString(0));

        return ids;
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
