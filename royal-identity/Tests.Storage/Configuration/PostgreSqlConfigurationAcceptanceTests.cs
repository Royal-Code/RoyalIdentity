using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RoyalIdentity.Storage.EntityFramework.Extensions;
using RoyalIdentity.Storage.EntityFramework.PostgreSql;
using Tests.Storage.Configuration.Support;

namespace Tests.Storage.Configuration;

/// <summary>Runs the provider acceptance scenarios introduced by Phases 4/5 against PostgreSQL.</summary>
public class PostgreSqlConfigurationAcceptanceTests
{
	[ConfigurationPostgreSqlFact]
	[Trait("Category", "PostgreSql")]
	public Task StoreAcceptances() => ProviderFactRunner.RunAsync(new PostgreSqlStoreAcceptances());

	[ConfigurationPostgreSqlFact]
	[Trait("Category", "PostgreSql")]
	public Task KeyAcceptances() => ProviderFactRunner.RunAsync(new PostgreSqlKeyAcceptances());

	[ConfigurationPostgreSqlFact]
	[Trait("Category", "PostgreSql")]
	public Task SnapshotAcceptances() => ProviderFactRunner.RunAsync(new PostgreSqlSnapshotAcceptances());

	private sealed class PostgreSqlStoreAcceptances
		: ConfigurationStoreProviderTests<ConfigurationPostgreSqlDbContext>
	{
		private protected override async Task<ConfigurationStorageHarness<ConfigurationPostgreSqlDbContext>>
			CreateHarnessAsync()
			=> await PostgreSqlConfigurationStorageHarness.CreateConcreteAsync();

		private protected override void AddProviderStorage(ServiceCollection services)
		{
			services.AddDbContext<ConfigurationPostgreSqlDbContext>(
				options => options.UseNpgsql("Host=model-only;Database=model-only"));
			services.AddEntityFrameworkConfigurationStorage<ConfigurationPostgreSqlDbContext>();
		}
	}

	private sealed class PostgreSqlKeyAcceptances
		: ConfigurationKeyStoreProviderTests<ConfigurationPostgreSqlDbContext>
	{
		private protected override async Task<ConfigurationStorageHarness<ConfigurationPostgreSqlDbContext>>
			CreateHarnessAsync()
			=> await PostgreSqlConfigurationStorageHarness.CreateConcreteAsync();
	}

	private sealed class PostgreSqlSnapshotAcceptances
		: ConfigurationSnapshotSourceProviderTests<ConfigurationPostgreSqlDbContext>
	{
		private protected override async Task<IConfigurationTestDatabase<ConfigurationPostgreSqlDbContext>>
			CreateDatabaseAsync()
			=> await PostgreSqlConfigurationDatabase.CreateMigratedAsync();
	}
}
