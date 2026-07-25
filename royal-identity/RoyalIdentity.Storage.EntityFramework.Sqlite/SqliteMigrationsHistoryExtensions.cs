using Microsoft.EntityFrameworkCore.Infrastructure;
using RoyalIdentity.Storage.EntityFramework.Migrations;

namespace RoyalIdentity.Storage.EntityFramework.Sqlite;

/// <summary>
/// Applies the SQLite migrations history topology of <see cref="StorageMigrationsHistory"/> (plan DF23).
/// SQLite has no schemas, so the two families are told apart by table name; every place that builds options for
/// migrations — the runner, the design-time factories and the test fixtures — calls one of these, so they
/// cannot drift apart.
/// </summary>
public static class SqliteMigrationsHistoryExtensions
{
    /// <summary>Points the Configuration family at <c>__ConfigurationMigrationsHistory</c>.</summary>
    public static SqliteDbContextOptionsBuilder UseConfigurationMigrationsHistory(
        this SqliteDbContextOptionsBuilder builder)
        => UseHistory(builder, StorageFamily.Configuration);

    /// <summary>Points the Operational family at <c>__OperationalMigrationsHistory</c>.</summary>
    public static SqliteDbContextOptionsBuilder UseOperationalMigrationsHistory(
        this SqliteDbContextOptionsBuilder builder)
        => UseHistory(builder, StorageFamily.Operational);

    private static SqliteDbContextOptionsBuilder UseHistory(
        SqliteDbContextOptionsBuilder builder, StorageFamily family)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var history = StorageMigrationsHistory.For(family, StorageProviderKind.Sqlite);

        return builder.MigrationsHistoryTable(history.Name, history.Schema);
    }
}
