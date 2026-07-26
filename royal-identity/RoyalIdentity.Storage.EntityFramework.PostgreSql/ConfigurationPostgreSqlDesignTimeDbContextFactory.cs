using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace RoyalIdentity.Storage.EntityFramework.PostgreSql;

/// <summary>Design-time-only factory for PostgreSQL migrations; it never opens the placeholder connection.</summary>
public sealed class ConfigurationPostgreSqlDesignTimeDbContextFactory
    : IDesignTimeDbContextFactory<ConfigurationPostgreSqlDbContext>
{
    public ConfigurationPostgreSqlDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<ConfigurationPostgreSqlDbContext>()
            .UseNpgsql(
                "Host=localhost;Database=royalidentity_design_time;Username=postgres;Password=not-used",
                // DF23: the history table is configured explicitly; the entities' schema does not imply it.
                npgsql => npgsql.UseConfigurationMigrationsHistory())
            .Options;

        return new ConfigurationPostgreSqlDbContext(options);
    }
}
