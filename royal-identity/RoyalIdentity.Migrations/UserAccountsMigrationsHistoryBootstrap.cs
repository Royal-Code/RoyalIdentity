using Microsoft.Data.Sqlite;
using Npgsql;
using RoyalIdentity.UserAccounts.Infrastructure.Data;

namespace RoyalIdentity.Migrations;

/// <summary>
/// Relocates the history of a UserAccounts database created before the module acquired its dedicated history
/// table. Rows are moved only when every legacy migration id belongs to UserAccounts; a mixed history is
/// ambiguous and fails closed.
/// </summary>
internal static class UserAccountsMigrationsHistoryBootstrap
{
    private const string LegacyHistoryTable = "__EFMigrationsHistory";

    public static Task RunAsync(
        MigrationRunnerOptions options,
        IReadOnlySet<string> userAccountsMigrations,
        CancellationToken ct)
        => options.ConfigurationProvider switch
        {
            ConfigurationDatabaseProvider.Sqlite => RunSqliteAsync(
                options.ResolvedUserAccountsConnection, userAccountsMigrations, ct),
            ConfigurationDatabaseProvider.PostgreSql => RunPostgreSqlAsync(
                options.ResolvedUserAccountsConnection, userAccountsMigrations, ct),
            _ => throw new InvalidOperationException("Unsupported UserAccounts provider."),
        };

    private static async Task RunSqliteAsync(
        string connectionString,
        IReadOnlySet<string> userAccountsMigrations,
        CancellationToken ct)
    {
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(ct);

        if (!await SqliteTableExistsAsync(connection, LegacyHistoryTable, ct))
            return;

        var legacyIds = await ReadSqliteMigrationIdsAsync(connection, ct);
        if (!ShouldRelocate(legacyIds, userAccountsMigrations))
            return;
        if (await SqliteTableExistsAsync(connection, UserAccountsDbContext.MigrationsHistoryTableName, ct))
            throw AmbiguousHistory();

        await using var rename = connection.CreateCommand();
        rename.CommandText =
            $"ALTER TABLE \"{LegacyHistoryTable}\" RENAME TO \"{UserAccountsDbContext.MigrationsHistoryTableName}\";";
        await rename.ExecuteNonQueryAsync(ct);
    }

    private static async Task RunPostgreSqlAsync(
        string connectionString,
        IReadOnlySet<string> userAccountsMigrations,
        CancellationToken ct)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(ct);

        if (!await PostgreSqlTableExistsAsync(connection, LegacyHistoryTable, ct))
            return;

        var legacyIds = await ReadPostgreSqlMigrationIdsAsync(connection, ct);
        if (!ShouldRelocate(legacyIds, userAccountsMigrations))
            return;
        if (await PostgreSqlTableExistsAsync(
            connection, UserAccountsDbContext.MigrationsHistoryTableName, ct))
        {
            throw AmbiguousHistory();
        }

        await using var rename = connection.CreateCommand();
        rename.CommandText =
            $"ALTER TABLE public.\"{LegacyHistoryTable}\" " +
            $"RENAME TO \"{UserAccountsDbContext.MigrationsHistoryTableName}\";";
        await rename.ExecuteNonQueryAsync(ct);
    }

    private static bool ShouldRelocate(
        IReadOnlyCollection<string> legacyIds,
        IReadOnlySet<string> userAccountsMigrations)
    {
        if (legacyIds.Count is 0)
            return false;

        var userAccountsCount = legacyIds.Count(userAccountsMigrations.Contains);
        if (userAccountsCount is 0)
            return false;
        if (userAccountsCount != legacyIds.Count)
            throw AmbiguousHistory();

        return true;
    }

    private static InvalidOperationException AmbiguousHistory()
        => new(
            $"The legacy '{LegacyHistoryTable}' and the dedicated " +
            $"'{UserAccountsDbContext.MigrationsHistoryTableName}' histories cannot be attributed to " +
            "UserAccounts unambiguously. Resolve the histories manually before migrating.");

    private static async Task<bool> SqliteTableExistsAsync(
        SqliteConnection connection,
        string table,
        CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = $name;";
        command.Parameters.AddWithValue("$name", table);
        return Convert.ToInt64(await command.ExecuteScalarAsync(ct)) > 0;
    }

    private static async Task<List<string>> ReadSqliteMigrationIdsAsync(
        SqliteConnection connection,
        CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT \"MigrationId\" FROM \"{LegacyHistoryTable}\";";
        await using var reader = await command.ExecuteReaderAsync(ct);
        List<string> ids = [];
        while (await reader.ReadAsync(ct))
            ids.Add(reader.GetString(0));

        return ids;
    }

    private static async Task<bool> PostgreSqlTableExistsAsync(
        NpgsqlConnection connection,
        string table,
        CancellationToken ct)
    {
        await using var command = new NpgsqlCommand(
            """
            SELECT COUNT(*)
            FROM information_schema.tables
            WHERE table_schema = 'public' AND table_name = @table;
            """,
            connection);
        command.Parameters.AddWithValue("table", table);
        return Convert.ToInt64(await command.ExecuteScalarAsync(ct)) > 0;
    }

    private static async Task<List<string>> ReadPostgreSqlMigrationIdsAsync(
        NpgsqlConnection connection,
        CancellationToken ct)
    {
        await using var command = new NpgsqlCommand(
            $"SELECT \"MigrationId\" FROM public.\"{LegacyHistoryTable}\";",
            connection);
        await using var reader = await command.ExecuteReaderAsync(ct);
        List<string> ids = [];
        while (await reader.ReadAsync(ct))
            ids.Add(reader.GetString(0));

        return ids;
    }
}
