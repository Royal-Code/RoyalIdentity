using Microsoft.EntityFrameworkCore;
using RoyalIdentity.Data.Configuration;

namespace RoyalIdentity.Storage.EntityFramework.Sqlite;

/// <summary>
/// SQLite default context for the Configuration family. It carries no mappings itself: the base
/// <see cref="ConfigurationDbContext.ApplyConfigurationModel"/> hook is overridden to call the single public
/// SQLite extension (plan DF3), the very same extension a third-party combined context would call. Migrations
/// are generated against this context; the host never applies them (plan DF11).
/// </summary>
public class ConfigurationSqliteDbContext : ConfigurationDbContext
{
	public ConfigurationSqliteDbContext(DbContextOptions<ConfigurationSqliteDbContext> options) : base(options)
	{
	}

	protected override void ApplyConfigurationModel(ModelBuilder modelBuilder)
		=> modelBuilder.ApplyRoyalIdentityConfigurationSqliteMappings();
}
