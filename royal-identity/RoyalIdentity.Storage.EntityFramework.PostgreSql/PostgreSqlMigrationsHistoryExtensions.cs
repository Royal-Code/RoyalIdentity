using Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure;
using RoyalIdentity.Storage.EntityFramework.Migrations;

namespace RoyalIdentity.Storage.EntityFramework.PostgreSql;

/// <summary>
/// Applies the PostgreSQL migrations history topology of <see cref="StorageMigrationsHistory"/> (plan DF23).
/// The two families keep EF's default table name and are told apart by schema — <c>configuration</c> and
/// <c>operation</c>.
/// <para>
/// Configuring it is not optional and not implied by the entities' schema: EF puts the history table in the
/// model's default schema, which neither family sets, so without these calls both families would land on
/// <c>public.__EFMigrationsHistory</c> and share one evolution line. Every place that builds options for
/// migrations — the runner, the design-time factories and the test fixtures — calls one of these.
/// </para>
/// </summary>
public static class PostgreSqlMigrationsHistoryExtensions
{
    /// <summary>Points the Configuration family at <c>configuration.__EFMigrationsHistory</c>.</summary>
    public static NpgsqlDbContextOptionsBuilder UseConfigurationMigrationsHistory(
        this NpgsqlDbContextOptionsBuilder builder)
        => UseHistory(builder, StorageFamily.Configuration);

    /// <summary>Points the Operational family at <c>operation.__EFMigrationsHistory</c>.</summary>
    public static NpgsqlDbContextOptionsBuilder UseOperationalMigrationsHistory(
        this NpgsqlDbContextOptionsBuilder builder)
        => UseHistory(builder, StorageFamily.Operational);

    private static NpgsqlDbContextOptionsBuilder UseHistory(
        NpgsqlDbContextOptionsBuilder builder, StorageFamily family)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var history = StorageMigrationsHistory.For(family, StorageProviderKind.PostgreSql);

        return builder.MigrationsHistoryTable(history.Name, history.Schema);
    }
}
