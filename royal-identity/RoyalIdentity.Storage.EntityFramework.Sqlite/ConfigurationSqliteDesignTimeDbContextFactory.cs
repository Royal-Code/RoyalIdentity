using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace RoyalIdentity.Storage.EntityFramework.Sqlite;

/// <summary>
/// Design-time factory used by <c>dotnet ef</c> tooling (migrations) to construct
/// <see cref="ConfigurationSqliteDbContext"/> without a host. The dummy connection string only selects the
/// SQLite provider for scaffolding — it is never opened, and migrations are never applied by the host
/// (plan DF11).
/// </summary>
public sealed class ConfigurationSqliteDesignTimeDbContextFactory
	: IDesignTimeDbContextFactory<ConfigurationSqliteDbContext>
{
	public ConfigurationSqliteDbContext CreateDbContext(string[] args)
	{
		var options = new DbContextOptionsBuilder<ConfigurationSqliteDbContext>()
			.UseSqlite("DataSource=design-time.db")
			.Options;

		return new ConfigurationSqliteDbContext(options);
	}
}
