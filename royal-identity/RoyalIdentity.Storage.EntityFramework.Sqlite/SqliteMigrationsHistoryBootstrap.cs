using Microsoft.Data.Sqlite;
using RoyalIdentity.Storage.EntityFramework.Migrations;

namespace RoyalIdentity.Storage.EntityFramework.Sqlite;

/// <summary>What the bootstrap found and did.</summary>
public enum MigrationsHistoryBootstrapOutcome
{
    /// <summary>Neither the legacy nor the Configuration history exists — a database that was never migrated.</summary>
    NoHistory,

    /// <summary>The legacy history existed and was moved, preserving every applied migration id.</summary>
    Relocated,

    /// <summary>Only the Configuration history exists — the move already happened, so this run is a no-op.</summary>
    AlreadyRelocated,
}

/// <summary>
/// <para>
///     Moves a SQLite database migrated by Plano 2 off EF's default <c>__EFMigrationsHistory</c> and onto the
///     Configuration history of <see cref="StorageMigrationsHistory"/> (plan DF23). It is infrastructure of the
///     runner, not a domain migration: it runs per connection <b>before</b> any <c>MigrateAsync</c>, so EF never
///     consults the new table while the old one still holds the applied ids — which would look like an empty
///     history and try to recreate every table.
/// </para>
/// <para>
///     The move preserves the migration ids verbatim, is idempotent, and fails closed when both histories exist:
///     that is ambiguity the tool cannot resolve, and merging or dropping either one silently could reapply or
///     skip migrations. Operational never shipped under the default name, so a legacy history on SQLite always
///     means Configuration.
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

        if (legacyExists && configurationExists)
        {
            throw new InvalidOperationException(
                $"Both '{legacy}' and '{configuration}' exist in this database. The migrations history is " +
                "ambiguous and will not be merged or dropped automatically; resolve it manually before migrating.");
        }

        if (!legacyExists)
            return configurationExists ? MigrationsHistoryBootstrapOutcome.AlreadyRelocated : MigrationsHistoryBootstrapOutcome.NoHistory;

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
}
