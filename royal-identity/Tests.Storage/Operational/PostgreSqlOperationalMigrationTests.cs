using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using RoyalIdentity.Data.Configuration.Entities;
using RoyalIdentity.Storage.EntityFramework.Migrations;
using RoyalIdentity.Storage.EntityFramework.PostgreSql;
using Tests.Storage.Configuration;
using Tests.Storage.Operational.Support;

namespace Tests.Storage.Operational;

/// <summary>
/// Proves the checked-in PostgreSQL Operational migration and the migrations history topology of plan DF23 over
/// a real PostgreSQL 17: each family records itself in its own schema's history, the two coexist in one
/// database, a database migrated by Plano 2 is relocated off <c>public</c> before anything else, and the
/// versioned SQL creates the same schema without running a host.
/// </summary>
public class PostgreSqlOperationalMigrationTests
{
    private static readonly string[] ExpectedOperationalTables =
    [
        "authorize_parameters",
        "consents",
        "protocol_artifacts",
        "user_session_clients",
        "user_sessions",
    ];

    [StoragePostgreSqlFact]
    [Trait("Category", "PostgreSql")]
    public async Task Migrate_CreatesTheOperationalTablesInTheOperationSchema()
    {
        await using var database = await PostgreSqlOperationalDatabase.CreateMigratedAsync();
        await using var connection = await OpenAsync(database);

        var operationTables = await TableNamesAsync(connection, "operation");
        var configurationTables = await TableNamesAsync(connection, "configuration");

        Assert.All(ExpectedOperationalTables, table => Assert.Contains(table, operationTables));
        // The Configuration tables stayed where they belong; sharing a database never merges the schemas.
        Assert.All(ExpectedOperationalTables, table => Assert.DoesNotContain(table, configurationTables));
        Assert.Contains("realms", configurationTables);
    }

    // DF23: sharing one database is the case that forces the split. On PostgreSQL the two histories keep EF's
    // default name and are told apart by schema, so both must exist and neither may live in `public`.
    [StoragePostgreSqlFact]
    [Trait("Category", "PostgreSql")]
    public async Task BothFamilies_InTheSameDatabase_KeepSeparateHistories()
    {
        await using var database = await PostgreSqlOperationalDatabase.CreateMigratedAsync();
        await using var connection = await OpenAsync(database);

        Assert.True(await TableExistsAsync(connection, "configuration", "__EFMigrationsHistory"));
        Assert.True(await TableExistsAsync(connection, "operation", "__EFMigrationsHistory"));
        Assert.False(await TableExistsAsync(connection, "public", "__EFMigrationsHistory"));

        await using var configuration = database.NewConfigurationContext();
        await using var operational = database.NewOperationalContext();

        var configurationApplied = await configuration.Database.GetAppliedMigrationsAsync();
        var operationalApplied = await operational.Database.GetAppliedMigrationsAsync();

        // Each family sees only its own evolution line, which is the whole point of the split.
        Assert.Contains(configurationApplied, id => id.EndsWith("_InitialConfiguration", StringComparison.Ordinal));
        Assert.DoesNotContain(configurationApplied, id => id.EndsWith("_InitialOperational", StringComparison.Ordinal));
        Assert.All(operationalApplied, id => Assert.EndsWith("_InitialOperational", id, StringComparison.Ordinal));
        Assert.Empty(await configuration.Database.GetPendingMigrationsAsync());
        Assert.Empty(await operational.Database.GetPendingMigrationsAsync());
    }

    // DF10: the collation is pinned in the migration, so uniqueness and lookups never follow the database locale.
    [StoragePostgreSqlFact]
    [Trait("Category", "PostgreSql")]
    public async Task Migrate_PinsTheOrdinalCollationOnIdentifiers()
    {
        await using var database = await PostgreSqlOperationalDatabase.CreateMigratedAsync();
        await using var connection = await OpenAsync(database);

        Assert.Equal("C", await CollationAsync(connection, "protocol_artifacts", "lookup_digest"));
        Assert.Equal("C", await CollationAsync(connection, "protocol_artifacts", "realm_id"));
        Assert.Equal("C", await CollationAsync(connection, "authorize_parameters", "handle_digest"));
        // Opaque payloads carry no collation: they are never compared.
        Assert.Null(await CollationAsync(connection, "protocol_artifacts", "protected_payload"));
    }

    [StoragePostgreSqlFact]
    [Trait("Category", "PostgreSql")]
    public async Task Bootstrap_OnAnEmptyDatabase_DoesNothing()
    {
        await using var database = await PostgreSqlOperationalDatabase.CreateEmptyAsync();

        var outcome = await new PostgreSqlMigrationsHistoryBootstrap().RunAsync(database.ConnectionString);

        Assert.Equal(MigrationsHistoryBootstrapOutcome.NoHistory, outcome);
    }

    [StoragePostgreSqlFact]
    [Trait("Category", "PostgreSql")]
    public async Task Bootstrap_WithOnlyForeignLegacyHistory_LeavesItForItsOwner()
    {
        await using var database = await PostgreSqlOperationalDatabase.CreateEmptyAsync();
        await using var connection = await OpenAsync(database);
        await CreateHistoryAsync(connection, "public", "20260721010432_InitialCreate");

        var outcome = await new PostgreSqlMigrationsHistoryBootstrap().RunAsync(connection);

        Assert.Equal(MigrationsHistoryBootstrapOutcome.ForeignHistory, outcome);
        Assert.Equal(["20260721010432_InitialCreate"], await MigrationIdsAsync(connection, "public"));
        Assert.False(await TableExistsAsync(connection, "configuration", "__EFMigrationsHistory"));
    }

    [StoragePostgreSqlFact]
    [Trait("Category", "PostgreSql")]
    public async Task Bootstrap_WithConfigurationAndForeignLegacyHistories_LeavesBothUntouched()
    {
        await using var database = await PostgreSqlOperationalDatabase.CreateEmptyAsync();
        await using var connection = await OpenAsync(database);
        await CreateHistoryAsync(connection, "public", "20260721010432_InitialCreate");
        await CreateHistoryAsync(connection, "configuration", "20260722233806_InitialConfiguration");

        var outcome = await new PostgreSqlMigrationsHistoryBootstrap().RunAsync(connection);

        Assert.Equal(MigrationsHistoryBootstrapOutcome.AlreadyRelocated, outcome);
        Assert.Equal(["20260721010432_InitialCreate"], await MigrationIdsAsync(connection, "public"));
        Assert.Equal(
            ["20260722233806_InitialConfiguration"],
            await MigrationIdsAsync(connection, "configuration"));
    }

    [StoragePostgreSqlFact]
    [Trait("Category", "PostgreSql")]
    public async Task Bootstrap_WithOnlyTheLegacyHistory_RelocatesItPreservingTheIds()
    {
        await using var database = await PostgreSqlOperationalDatabase.CreateEmptyAsync();
        await using var connection = await OpenAsync(database);
        await CreateHistoryAsync(connection, "public", "20260722233806_InitialConfiguration");

        var outcome = await new PostgreSqlMigrationsHistoryBootstrap().RunAsync(connection);

        Assert.Equal(MigrationsHistoryBootstrapOutcome.Relocated, outcome);
        Assert.Equal(
            ["20260722233806_InitialConfiguration"],
            await MigrationIdsAsync(connection, "configuration"));
        Assert.False(await TableExistsAsync(connection, "public", "__EFMigrationsHistory"));
    }

    [StoragePostgreSqlFact]
    [Trait("Category", "PostgreSql")]
    public async Task Bootstrap_WithOnlyTheNewHistory_IsANoOp()
    {
        await using var database = await PostgreSqlOperationalDatabase.CreateEmptyAsync();
        await using var connection = await OpenAsync(database);
        await CreateHistoryAsync(connection, "configuration", "20260722233806_InitialConfiguration");

        var outcome = await new PostgreSqlMigrationsHistoryBootstrap().RunAsync(connection);

        Assert.Equal(MigrationsHistoryBootstrapOutcome.AlreadyRelocated, outcome);
        Assert.Equal(
            ["20260722233806_InitialConfiguration"],
            await MigrationIdsAsync(connection, "configuration"));
    }

    // Ambiguity fails closed: merging or dropping either table would silently reapply or skip migrations.
    [StoragePostgreSqlFact]
    [Trait("Category", "PostgreSql")]
    public async Task Bootstrap_WithBothHistories_FailsClosedWithoutTouchingEither()
    {
        await using var database = await PostgreSqlOperationalDatabase.CreateEmptyAsync();
        await using var connection = await OpenAsync(database);
        await CreateHistoryAsync(connection, "public", "20260722233806_InitialConfiguration");
        await CreateHistoryAsync(connection, "configuration", "20260101000000_Other");

        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await new PostgreSqlMigrationsHistoryBootstrap().RunAsync(connection));

        Assert.Equal(["20260722233806_InitialConfiguration"], await MigrationIdsAsync(connection, "public"));
        Assert.Equal(["20260101000000_Other"], await MigrationIdsAsync(connection, "configuration"));
    }

    [StoragePostgreSqlFact]
    [Trait("Category", "PostgreSql")]
    public async Task Bootstrap_WithMixedLegacyHistory_FailsClosedWithoutTouchingIt()
    {
        await using var database = await PostgreSqlOperationalDatabase.CreateEmptyAsync();
        await using var connection = await OpenAsync(database);
        await CreateHistoryAsync(connection, "public", "20260722233806_InitialConfiguration");
        await AddHistoryIdAsync(connection, "public", "20260721010432_InitialCreate");

        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await new PostgreSqlMigrationsHistoryBootstrap().RunAsync(connection));

        Assert.Equal(
            ["20260721010432_InitialCreate", "20260722233806_InitialConfiguration"],
            await MigrationIdsAsync(connection, "public"));
        Assert.False(await TableExistsAsync(connection, "configuration", "__EFMigrationsHistory"));
    }

    [StoragePostgreSqlFact]
    [Trait("Category", "PostgreSql")]
    public async Task Bootstrap_RepeatedAfterRelocating_IsIdempotent()
    {
        await using var database = await PostgreSqlOperationalDatabase.CreateEmptyAsync();
        await using var connection = await OpenAsync(database);
        await CreateHistoryAsync(connection, "public", "20260722233806_InitialConfiguration");
        var bootstrap = new PostgreSqlMigrationsHistoryBootstrap();

        Assert.Equal(MigrationsHistoryBootstrapOutcome.Relocated, await bootstrap.RunAsync(connection));
        Assert.Equal(MigrationsHistoryBootstrapOutcome.AlreadyRelocated, await bootstrap.RunAsync(connection));
        Assert.Equal(MigrationsHistoryBootstrapOutcome.AlreadyRelocated, await bootstrap.RunAsync(connection));

        Assert.Equal(
            ["20260722233806_InitialConfiguration"],
            await MigrationIdsAsync(connection, "configuration"));
    }

    // After the bootstrap, EF sees the database as already migrated instead of trying to recreate it — which is
    // exactly what a Plano 2 PostgreSQL database upgrading to this plan goes through.
    [StoragePostgreSqlFact]
    [Trait("Category", "PostgreSql")]
    public async Task Bootstrap_ThenMigrate_UpgradesAPlano2Database_PreservingItsRows()
    {
        await using var database = await PostgreSqlOperationalDatabase.CreateEmptyAsync();

        // A database exactly as Plano 2 left it: the initial migration only — this plan's drop has not run —
        // recorded under EF's default history, in `public`.
        await using (var legacy = new ConfigurationPostgreSqlDbContext(
            new DbContextOptionsBuilder<ConfigurationPostgreSqlDbContext>()
                .UseNpgsql(database.ConnectionString)
                .Options))
        {
            await legacy.GetService<IMigrator>().MigrateAsync("20260722233806_InitialConfiguration");
        }

        await using var connection = await OpenAsync(database);
        Assert.True(await TableExistsAsync(connection, "public", "__EFMigrationsHistory"));
        // Plano 2 shipped the column this plan removes; seeing it here is what makes the upgrade real.
        Assert.True(await ColumnExistsAsync(connection, "clients", "update_access_token_claims_on_refresh"));
        await SeedClientAsync(database, connection);

        Assert.Equal(
            MigrationsHistoryBootstrapOutcome.Relocated,
            await new PostgreSqlMigrationsHistoryBootstrap().RunAsync(connection));

        await using (var upgraded = database.NewConfigurationContext())
        {
            // Only the pending migration runs: the initial one is already recorded in the relocated history.
            Assert.Equal(
                ["20260726011051_DropUpdateAccessTokenClaimsOnRefresh"],
                await upgraded.Database.GetPendingMigrationsAsync());
            await upgraded.Database.MigrateAsync();
            Assert.Empty(await upgraded.Database.GetPendingMigrationsAsync());
        }

        // The client survived the upgrade; only the obsolete column is gone.
        Assert.False(await ColumnExistsAsync(connection, "clients", "update_access_token_claims_on_refresh"));
        Assert.Equal(1L, await ScalarAsync<long>(connection, "SELECT COUNT(*) FROM configuration.clients"));
    }

    [StoragePostgreSqlFact]
    [Trait("Category", "PostgreSql")]
    public async Task GeneratedSqlScripts_CreateTheSameSchema_AndAreIdempotent()
    {
        await using var database = await PostgreSqlOperationalDatabase.CreateEmptyAsync();
        await using var connection = await OpenAsync(database);

        var script = await File.ReadAllTextAsync(Path.Combine(
            FindRepositoryRoot(), "scripts", "sql", "operational", "postgresql", "0001_initial_operational.sql"));

        await using (var command = new NpgsqlCommand(script, connection) { CommandTimeout = 60 })
        {
            // The production script is idempotent: once the history row is present, repeating it is a no-op.
            await command.ExecuteNonQueryAsync();
            await command.ExecuteNonQueryAsync();
        }

        var tables = await TableNamesAsync(connection, "operation");
        Assert.All(ExpectedOperationalTables, table => Assert.Contains(table, tables));
        Assert.True(await TableExistsAsync(connection, "operation", "__EFMigrationsHistory"));

        // And EF agrees the database is up to date, without ever running a host.
        await using var context = database.NewOperationalContext();
        Assert.Empty(await context.Database.GetPendingMigrationsAsync());
    }

    // The bootstrap SQL is the reviewable equivalent of the code path, so it has to behave the same way.
    [StoragePostgreSqlFact]
    [Trait("Category", "PostgreSql")]
    public async Task BootstrapSqlScript_RelocatesTheLegacyHistory_AndIsIdempotent()
    {
        await using var database = await PostgreSqlOperationalDatabase.CreateEmptyAsync();
        await using var connection = await OpenAsync(database);
        await CreateHistoryAsync(connection, "public", "20260722233806_InitialConfiguration");

        var script = await File.ReadAllTextAsync(Path.Combine(
            FindRepositoryRoot(),
            "scripts", "sql", "migration-history", "postgresql",
            "0001_relocate_legacy_configuration_history.sql"));

        await using (var command = new NpgsqlCommand(script, connection))
        {
            await command.ExecuteNonQueryAsync();
            await command.ExecuteNonQueryAsync();
        }

        Assert.False(await TableExistsAsync(connection, "public", "__EFMigrationsHistory"));
        Assert.Equal(
            ["20260722233806_InitialConfiguration"],
            await MigrationIdsAsync(connection, "configuration"));
    }

    private static async Task<NpgsqlConnection> OpenAsync(PostgreSqlOperationalDatabase database)
    {
        var connection = new NpgsqlConnection(database.ConnectionString);
        await connection.OpenAsync();

        return connection;
    }

    private static async Task<List<string>> TableNamesAsync(NpgsqlConnection connection, string schema)
    {
        var tables = new List<string>();
        await using var command = new NpgsqlCommand(
            "SELECT table_name FROM information_schema.tables WHERE table_schema = @schema ORDER BY table_name",
            connection);
        command.Parameters.AddWithValue("schema", schema);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            tables.Add(reader.GetString(0));

        return tables;
    }

    private static async Task<bool> TableExistsAsync(NpgsqlConnection connection, string schema, string table)
        => await ScalarAsync<long>(
            connection,
            "SELECT COUNT(*) FROM information_schema.tables " +
            $"WHERE table_schema = '{schema}' AND table_name = '{table}'") > 0;

    private static async Task<bool> ColumnExistsAsync(NpgsqlConnection connection, string table, string column)
        => await ScalarAsync<long>(
            connection,
            "SELECT COUNT(*) FROM information_schema.columns " +
            $"WHERE table_schema = 'configuration' AND table_name = '{table}' AND column_name = '{column}'") > 0;

    private static async Task<string?> CollationAsync(NpgsqlConnection connection, string table, string column)
    {
        await using var command = new NpgsqlCommand(
            "SELECT collation_name FROM information_schema.columns " +
            "WHERE table_schema = 'operation' AND table_name = @table AND column_name = @column",
            connection);
        command.Parameters.AddWithValue("table", table);
        command.Parameters.AddWithValue("column", column);

        return await command.ExecuteScalarAsync() as string;
    }

    private static async Task<T> ScalarAsync<T>(NpgsqlConnection connection, string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection);

        return (T)Convert.ChangeType(await command.ExecuteScalarAsync(), typeof(T))!;
    }

    private static async Task CreateHistoryAsync(NpgsqlConnection connection, string schema, string migrationId)
    {
        await using var command = new NpgsqlCommand(
            $"CREATE SCHEMA IF NOT EXISTS \"{schema}\"; " +
            $"CREATE TABLE \"{schema}\".\"__EFMigrationsHistory\" (" +
            "\"MigrationId\" character varying(150) NOT NULL, " +
            "\"ProductVersion\" character varying(32) NOT NULL, " +
            $"CONSTRAINT \"PK___EFMigrationsHistory_{schema}\" PRIMARY KEY (\"MigrationId\")); " +
            $"INSERT INTO \"{schema}\".\"__EFMigrationsHistory\" VALUES (@id, '10.0.10');",
            connection);
        command.Parameters.AddWithValue("id", migrationId);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task AddHistoryIdAsync(
        NpgsqlConnection connection,
        string schema,
        string migrationId)
    {
        await using var command = new NpgsqlCommand(
            $"INSERT INTO \"{schema}\".\"__EFMigrationsHistory\" VALUES (@id, '10.0.10');",
            connection);
        command.Parameters.AddWithValue("id", migrationId);
        await command.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Writes a realm and a client into the Plano 2 schema through the current model. The obsolete column is
    /// given a temporary default first, because the model no longer knows about it — which is precisely the
    /// situation the pending migration exists to resolve, and it keeps this seed from having to restate the
    /// whole Plano 2 column list.
    /// </summary>
    private static async Task SeedClientAsync(
        PostgreSqlOperationalDatabase database, NpgsqlConnection connection)
    {
        await using (var command = new NpgsqlCommand(
            "ALTER TABLE configuration.clients " +
            "ALTER COLUMN update_access_token_claims_on_refresh SET DEFAULT false;",
            connection))
        {
            await command.ExecuteNonQueryAsync();
        }

        await using var context = database.NewConfigurationContext();
        context.Realms.Add(new RealmEntity
        {
            Id = "upgrade-realm",
            Path = "upgrade",
            Domain = "upgrade.test",
            DisplayName = "Upgrade Realm",
            Enabled = true,
            Internal = false,
            OptionsVersion = 1,
            OptionsJson = "{}",
        });
        context.Clients.Add(new ClientEntity
        {
            RealmId = "upgrade-realm",
            ClientId = "upgrade-client",
            Name = "Upgrade Client",
            ProtocolType = "oidc",
            Enabled = true,
        });
        await context.SaveChangesAsync();
    }

    private static async Task<List<string>> MigrationIdsAsync(NpgsqlConnection connection, string schema)
    {
        var ids = new List<string>();
        await using var command = new NpgsqlCommand(
            $"SELECT \"MigrationId\" FROM \"{schema}\".\"__EFMigrationsHistory\" ORDER BY \"MigrationId\"",
            connection);
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
