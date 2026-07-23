using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RoyalIdentity.Storage.EntityFramework.Extensions;
using RoyalIdentity.Storage.EntityFramework.PostgreSql;
using Tests.Storage.Support;

namespace Tests.Storage.Configuration.Support;

/// <summary>PostgreSQL specialization of the shared Configuration contract harness.</summary>
internal sealed class PostgreSqlConfigurationStorageHarness
	: ConfigurationStorageHarness<ConfigurationPostgreSqlDbContext>
{
	private PostgreSqlConfigurationStorageHarness(HarnessState state) : base(state)
	{
	}

	public static async Task<StorageContractHarness> CreateAsync()
		=> await CreateConcreteAsync();

	internal static async Task<PostgreSqlConfigurationStorageHarness> CreateConcreteAsync()
	{
		var database = await PostgreSqlConfigurationDatabase.CreateMigratedAsync();
		return await CreateCoreAsync(
			database,
			services =>
			{
				services.AddDbContext<ConfigurationPostgreSqlDbContext>(
					options => options.UseNpgsql(database.ConnectionString));
				services.AddEntityFrameworkConfigurationStorage<ConfigurationPostgreSqlDbContext>();
			},
			state => new PostgreSqlConfigurationStorageHarness(state));
	}
}
