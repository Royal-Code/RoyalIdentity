using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace RoyalIdentity.Storage.EntityFramework.PostgreSql;

/// <summary>
/// Design-time-only factory used by <c>dotnet ef</c> to construct <see cref="OperationalPostgreSqlDbContext"/>
/// without a host. The placeholder connection string only selects the Npgsql provider for scaffolding — it is
/// never opened, and migrations are never applied by the host (plan DF23). The history table comes from the
/// centralized topology, so Configuration and Operational never share an evolution line even when they share a
/// database.
/// </summary>
public sealed class OperationalPostgreSqlDesignTimeDbContextFactory
    : IDesignTimeDbContextFactory<OperationalPostgreSqlDbContext>
{
    public OperationalPostgreSqlDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<OperationalPostgreSqlDbContext>()
            .UseNpgsql(
                "Host=localhost;Database=royalidentity_design_time;Username=postgres;Password=not-used",
                npgsql => npgsql.UseOperationalMigrationsHistory())
            .Options;

        return new OperationalPostgreSqlDbContext(options);
    }
}
