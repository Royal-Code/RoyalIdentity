namespace RoyalIdentity.Storage.EntityFramework.Migrations;

/// <summary>The storage families that evolve independently and therefore keep independent history tables.</summary>
public enum StorageFamily
{
    Configuration,
    Operational
}

/// <summary>The relational providers this product ships migrations for.</summary>
public enum StorageProviderKind
{
    Sqlite,
    PostgreSql
}

/// <summary>A migrations history table: its name and, where the provider has schemas, its schema.</summary>
/// <param name="Name">The table name.</param>
/// <param name="Schema">The schema, or <c>null</c> for the provider default / a provider without schemas.</param>
public sealed record MigrationsHistoryTable(string Name, string? Schema);

/// <summary>
/// <para>
///     Single source of the migrations history topology (plan-data-operational-storage DF23). Configuration
///     and Operational may share one database, so neither family may rely on EF's default
///     <c>__EFMigrationsHistory</c>: on PostgreSQL the same name is isolated by the <c>configuration</c> and
///     <c>operation</c> schemas; on SQLite, which has no schemas, the names themselves differ.
/// </para>
/// <para>
///     Every place that builds options for migrations — the runner, the design-time factories and the test
///     fixtures — reads the topology from here, so they cannot drift apart. Moving a database migrated by
///     Plano 2 out of the legacy history is a runner bootstrap concern (it runs before any
///     <c>MigrateAsync</c>), not a domain migration; <see cref="Legacy"/> names the table it starts from.
/// </para>
/// </summary>
public static class StorageMigrationsHistory
{
    /// <summary>EF's default history table, used by Configuration before this topology existed.</summary>
    public static MigrationsHistoryTable Legacy { get; } = new("__EFMigrationsHistory", null);

    /// <summary>Configuration history on SQLite.</summary>
    public static MigrationsHistoryTable ConfigurationSqlite { get; } = new("__ConfigurationMigrationsHistory", null);

    /// <summary>Operational history on SQLite.</summary>
    public static MigrationsHistoryTable OperationalSqlite { get; } = new("__OperationalMigrationsHistory", null);

    /// <summary>Configuration history on PostgreSQL, isolated by the <c>configuration</c> schema.</summary>
    public static MigrationsHistoryTable ConfigurationPostgreSql { get; } = new(Legacy.Name, "configuration");

    /// <summary>Operational history on PostgreSQL, isolated by the <c>operation</c> schema.</summary>
    public static MigrationsHistoryTable OperationalPostgreSql { get; } = new(Legacy.Name, "operation");

    /// <summary>Gets the history table of a family on a provider.</summary>
    public static MigrationsHistoryTable For(StorageFamily family, StorageProviderKind provider)
        => (family, provider) switch
        {
            (StorageFamily.Configuration, StorageProviderKind.Sqlite) => ConfigurationSqlite,
            (StorageFamily.Configuration, StorageProviderKind.PostgreSql) => ConfigurationPostgreSql,
            (StorageFamily.Operational, StorageProviderKind.Sqlite) => OperationalSqlite,
            (StorageFamily.Operational, StorageProviderKind.PostgreSql) => OperationalPostgreSql,
            _ => throw new ArgumentOutOfRangeException(nameof(provider), provider, "Unknown storage provider."),
        };
}
