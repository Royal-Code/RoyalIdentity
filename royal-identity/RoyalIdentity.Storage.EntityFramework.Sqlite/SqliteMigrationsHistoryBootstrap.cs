using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using RoyalIdentity.Storage.EntityFramework.Migrations;

namespace RoyalIdentity.Storage.EntityFramework.Sqlite;

/// <summary>
/// <para>
///     Moves a SQLite database migrated by Plano 2 off EF's default <c>__EFMigrationsHistory</c> and onto the
///     Configuration history of <see cref="StorageMigrationsHistory"/> (plan DF23). It is infrastructure of the
///     runner, not a domain migration: it runs per connection <b>before</b> any <c>MigrateAsync</c>, so EF never
///     consults the new table while the old one still holds the applied ids — which would look like an empty
///     history and try to recreate every table.
/// </para>
/// <para>
///     UserAccounts shipped under EF's default history before acquiring its own history table. Therefore this
///     bootstrap inspects every legacy migration id: it moves the table only when all ids belong to Configuration,
///     leaves a history owned entirely by another family untouched, and fails closed when ownership is mixed or
///     when both Configuration histories exist.
/// </para>
/// </summary>
public sealed class SqliteMigrationsHistoryBootstrap
{
    /// <summary>Runs the bootstrap over an already-open connection.</summary>
    public async Task<MigrationsHistoryBootstrapOutcome> RunAsync(
        SqliteConnection connection, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(connection);

        if (connection.State is not System.Data.ConnectionState.Open)
            await connection.OpenAsync(ct);

        var legacy = StorageMigrationsHistory.Legacy.Name;
        var configuration = StorageMigrationsHistory
            .For(StorageFamily.Configuration, StorageProviderKind.Sqlite).Name;

        var legacyExists = await TableExistsAsync(connection, legacy, ct);
        var configurationExists = await TableExistsAsync(connection, configuration, ct);

        if (!legacyExists)
        {
            return configurationExists
                ? MigrationsHistoryBootstrapOutcome.AlreadyRelocated
                : MigrationsHistoryBootstrapOutcome.NoHistory;
        }

        var legacyIds = await MigrationIdsAsync(connection, legacy, ct);
        var ownedCount = legacyIds.Count(ConfigurationMigrationIds.Contains);
        if (ownedCount is 0)
        {
            return configurationExists
                ? MigrationsHistoryBootstrapOutcome.AlreadyRelocated
                : MigrationsHistoryBootstrapOutcome.ForeignHistory;
        }
        if (ownedCount != legacyIds.Count)
        {
            throw new InvalidOperationException(
                $"The legacy '{legacy}' contains migration ids from Configuration and another family. The " +
                "migrations history is ambiguous and will not be split automatically; resolve it manually " +
                "before migrating.");
        }
        if (configurationExists)
        {
            throw new InvalidOperationException(
                $"Both '{legacy}' and '{configuration}' exist in this database. The migrations history is " +
                "ambiguous and will not be merged or dropped automatically; resolve it manually before migrating.");
        }

        await using var command = connection.CreateCommand();
        // ALTER TABLE ... RENAME TO keeps every row, so the applied migration ids are preserved exactly.
        command.CommandText = $"ALTER TABLE \"{legacy}\" RENAME TO \"{configuration}\";";
        await command.ExecuteNonQueryAsync(ct);

        return MigrationsHistoryBootstrapOutcome.Relocated;
    }

    /// <summary>Runs the bootstrap over its own connection, for callers that only hold a connection string.</summary>
    public async Task<MigrationsHistoryBootstrapOutcome> RunAsync(
        string connectionString, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(ct);

        return await RunAsync(connection, ct);
    }

    private static async Task<bool> TableExistsAsync(
        SqliteConnection connection, string tableName, CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = $name;";
        command.Parameters.AddWithValue("$name", tableName);

        return Convert.ToInt64(await command.ExecuteScalarAsync(ct)) > 0;
    }

    private static async Task<List<string>> MigrationIdsAsync(
        SqliteConnection connection,
        string table,
        CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT \"MigrationId\" FROM \"{table}\";";
        await using var reader = await command.ExecuteReaderAsync(ct);
        List<string> ids = [];
        while (await reader.ReadAsync(ct))
            ids.Add(reader.GetString(0));

        return ids;
    }

    private static readonly IReadOnlySet<string> ConfigurationMigrationIds = GetConfigurationMigrationIds();

    private static IReadOnlySet<string> GetConfigurationMigrationIds()
    {
        using var context = new ConfigurationSqliteDbContext(
            new DbContextOptionsBuilder<ConfigurationSqliteDbContext>()
                .UseSqlite("Data Source=:memory:")
                .Options);
        return context.Database.GetMigrations().ToHashSet(StringComparer.Ordinal);
    }
}
