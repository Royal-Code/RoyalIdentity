using Microsoft.EntityFrameworkCore;
using RoyalIdentity.Storage.EntityFramework.PostgreSql;
using RoyalIdentity.Storage.EntityFramework.Sqlite;

namespace Tests.Storage.Operational;

/// <summary>
/// The automated equivalent of <c>dotnet ef migrations has-pending-model-changes</c>, for every context this
/// product ships migrations for (plan DF23). It answers a question <c>GetPendingMigrationsAsync</c> cannot:
/// that one compares the applied history against the migrations on disk, while this compares the <b>model</b>
/// against the snapshot the last migration recorded. A mapping changed without a migration passes the first and
/// fails this one — which is exactly the drift that ships a schema the code no longer matches.
/// <para>
/// No connection is opened: only the model and the snapshot are compared.
/// </para>
/// </summary>
public class MigrationsSnapshotDriftTests
{
    [Fact]
    public void ConfigurationSqlite_HasNoPendingModelChanges()
    {
        using var context = new ConfigurationSqliteDbContext(
            new DbContextOptionsBuilder<ConfigurationSqliteDbContext>()
                .UseSqlite("Data Source=model-only.db", sqlite => sqlite.UseConfigurationMigrationsHistory())
                .Options);

        Assert.False(context.Database.HasPendingModelChanges());
    }

    [Fact]
    public void OperationalSqlite_HasNoPendingModelChanges()
    {
        using var context = new OperationalSqliteDbContext(
            new DbContextOptionsBuilder<OperationalSqliteDbContext>()
                .UseSqlite("Data Source=model-only.db", sqlite => sqlite.UseOperationalMigrationsHistory())
                .Options);

        Assert.False(context.Database.HasPendingModelChanges());
    }

    [Fact]
    public void ConfigurationPostgreSql_HasNoPendingModelChanges()
    {
        using var context = new ConfigurationPostgreSqlDbContext(
            new DbContextOptionsBuilder<ConfigurationPostgreSqlDbContext>()
                .UseNpgsql("Host=model-only;Database=model-only", npgsql => npgsql.UseConfigurationMigrationsHistory())
                .Options);

        Assert.False(context.Database.HasPendingModelChanges());
    }

    [Fact]
    public void OperationalPostgreSql_HasNoPendingModelChanges()
    {
        using var context = new OperationalPostgreSqlDbContext(
            new DbContextOptionsBuilder<OperationalPostgreSqlDbContext>()
                .UseNpgsql("Host=model-only;Database=model-only", npgsql => npgsql.UseOperationalMigrationsHistory())
                .Options);

        Assert.False(context.Database.HasPendingModelChanges());
    }
}
