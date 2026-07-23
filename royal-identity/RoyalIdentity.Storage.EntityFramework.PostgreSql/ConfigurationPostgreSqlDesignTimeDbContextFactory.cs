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
			.UseNpgsql("Host=localhost;Database=royalidentity_design_time;Username=postgres;Password=not-used")
			.Options;

		return new ConfigurationPostgreSqlDbContext(options);
	}
}
