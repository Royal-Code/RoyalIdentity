using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace RoyalIdentity.Storage.EntityFramework.Sqlite;

/// <summary>
/// Design-time factory used by <c>dotnet ef</c> tooling to construct <see cref="OperationalSqliteDbContext"/>
/// without a host. The dummy connection string only selects the SQLite provider for scaffolding — it is never
/// opened, and migrations are never applied by the host (plan DF23). The Operational history table comes from
/// the centralized topology, so Configuration and Operational never share an evolution line even when they
/// share a database file.
/// </summary>
public sealed class OperationalSqliteDesignTimeDbContextFactory
    : IDesignTimeDbContextFactory<OperationalSqliteDbContext>
{
    public OperationalSqliteDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<OperationalSqliteDbContext>()
            .UseSqlite("DataSource=design-time.db", sqlite => sqlite.UseOperationalMigrationsHistory())
            .Options;

        return new OperationalSqliteDbContext(options);
    }
}
