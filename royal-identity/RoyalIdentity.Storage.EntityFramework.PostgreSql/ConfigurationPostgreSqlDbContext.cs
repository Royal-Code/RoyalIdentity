using Microsoft.EntityFrameworkCore;
using RoyalIdentity.Data.Configuration;

namespace RoyalIdentity.Storage.EntityFramework.PostgreSql;

/// <summary>
/// PostgreSQL default context for Configuration. Mappings remain in the public provider extension so a
/// third-party combined context can apply exactly the same model (plan DF3/DF18).
/// </summary>
public class ConfigurationPostgreSqlDbContext : ConfigurationDbContext
{
	public ConfigurationPostgreSqlDbContext(DbContextOptions<ConfigurationPostgreSqlDbContext> options)
		: base(options)
	{
	}

	protected override void ApplyConfigurationModel(ModelBuilder modelBuilder)
		=> modelBuilder.ApplyRoyalIdentityConfigurationPostgreSqlMappings();
}
