using Microsoft.EntityFrameworkCore;
using Npgsql;
using RoyalIdentity.Storage.EntityFramework.Migrations;

namespace RoyalIdentity.Storage.EntityFramework.PostgreSql;

/// <summary>
/// <para>
///     Moves a PostgreSQL database migrated by Plano 2 off EF's default <c>public.__EFMigrationsHistory</c> and
///     into the Configuration history of <see cref="StorageMigrationsHistory"/> (plan DF23). On PostgreSQL both
///     families keep the default table <i>name</i>, so what tells them apart is the schema — and the schema of
///     the entities does not configure the history table, which is why a Plano 2 database has it in
///     <c>public</c> while the tables themselves live in <c>configuration</c>.
/// </para>
/// <para>
///     Like its SQLite counterpart it is infrastructure of the runner, not a domain migration: it runs per
///     connection <b>before</b> any <c>MigrateAsync</c>, so EF never consults the new location while the old one
///     still holds the applied ids — which would look like an empty history and try to recreate every table.
///     UserAccounts shipped under <c>public.__EFMigrationsHistory</c> before acquiring its own history table.
///     Therefore this bootstrap inspects every legacy migration id: it moves the table only when all ids belong
///     to Configuration, leaves a history owned entirely by another family untouched, and fails closed when
///     ownership is mixed or when both Configuration histories exist.
/// </para>
/// </summary>
public sealed class PostgreSqlMigrationsHistoryBootstrap
{
    /// <summary>The schema EF falls back to when the model declares no default schema.</summary>
    private const string LegacySchema = "public";

    /// <summary>Runs the bootstrap over an already-open connection.</summary>
    public async Task<MigrationsHistoryBootstrapOutcome> RunAsync(
        NpgsqlConnection connection, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(connection);

        if (connection.State is not System.Data.ConnectionState.Open)
            await connection.OpenAsync(ct);

        var target = StorageMigrationsHistory.For(StorageFamily.Configuration, StorageProviderKind.PostgreSql);
        var table = target.Name;
        var targetSchema = target.Schema
            ?? throw new InvalidOperationException("The PostgreSQL Configuration history must declare a schema.");

        var legacyExists = await TableExistsAsync(connection, LegacySchema, table, ct);
        var targetExists = await TableExistsAsync(connection, targetSchema, table, ct);

        if (!legacyExists)
        {
            return targetExists
                ? MigrationsHistoryBootstrapOutcome.AlreadyRelocated
                : MigrationsHistoryBootstrapOutcome.NoHistory;
        }

        var legacyIds = await MigrationIdsAsync(connection, LegacySchema, table, ct);
        var ownedCount = legacyIds.Count(ConfigurationMigrationIds.Contains);
        if (ownedCount is 0)
        {
            return targetExists
                ? MigrationsHistoryBootstrapOutcome.AlreadyRelocated
                : MigrationsHistoryBootstrapOutcome.ForeignHistory;
        }
        if (ownedCount != legacyIds.Count)
        {
            throw new InvalidOperationException(
                $"The legacy '{LegacySchema}.{table}' contains migration ids from Configuration and another " +
                "family. The migrations history is ambiguous and will not be split automatically; resolve it " +
                "manually before migrating.");
        }
        if (targetExists)
        {
            throw new InvalidOperationException(
                $"Both '{LegacySchema}.{table}' and '{targetSchema}.{table}' exist in this database. The " +
                "migrations history is ambiguous and will not be merged or dropped automatically; resolve it " +
                "manually before migrating.");
        }

        await using (var createSchema = connection.CreateCommand())
        {
            createSchema.CommandText = $"CREATE SCHEMA IF NOT EXISTS \"{targetSchema}\";";
            await createSchema.ExecuteNonQueryAsync(ct);
        }

        await using (var move = connection.CreateCommand())
        {
            // SET SCHEMA keeps every row, so the applied migration ids are preserved exactly.
            move.CommandText =
                $"ALTER TABLE \"{LegacySchema}\".\"{table}\" SET SCHEMA \"{targetSchema}\";";
            await move.ExecuteNonQueryAsync(ct);
        }

        return MigrationsHistoryBootstrapOutcome.Relocated;
    }

    /// <summary>Runs the bootstrap over its own connection, for callers that only hold a connection string.</summary>
    public async Task<MigrationsHistoryBootstrapOutcome> RunAsync(
        string connectionString, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(ct);

        return await RunAsync(connection, ct);
    }

    private static async Task<bool> TableExistsAsync(
        NpgsqlConnection connection, string schema, string table, CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT COUNT(*) FROM information_schema.tables " +
            "WHERE table_schema = @schema AND table_name = @table;";
        command.Parameters.AddWithValue("schema", schema);
        command.Parameters.AddWithValue("table", table);

        return Convert.ToInt64(await command.ExecuteScalarAsync(ct)) > 0;
    }

    private static async Task<List<string>> MigrationIdsAsync(
        NpgsqlConnection connection,
        string schema,
        string table,
        CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT \"MigrationId\" FROM \"{schema}\".\"{table}\";";
        await using var reader = await command.ExecuteReaderAsync(ct);
        List<string> ids = [];
        while (await reader.ReadAsync(ct))
            ids.Add(reader.GetString(0));

        return ids;
    }

    private static readonly IReadOnlySet<string> ConfigurationMigrationIds = GetConfigurationMigrationIds();

    private static IReadOnlySet<string> GetConfigurationMigrationIds()
    {
        using var context = new ConfigurationPostgreSqlDbContext(
            new DbContextOptionsBuilder<ConfigurationPostgreSqlDbContext>()
                .UseNpgsql("Host=design-time;Database=design-time")
                .Options);
        return context.Database.GetMigrations().ToHashSet(StringComparer.Ordinal);
    }
}
