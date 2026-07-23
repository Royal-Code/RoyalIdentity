using Tests.Storage.Configuration.Support;
using Tests.Storage.Contracts;
using Tests.Storage.Support;

namespace Tests.Storage.Configuration;

/// <summary>
/// Runs the exact provider-neutral P2 scenarios against PostgreSQL. The concrete suites stay private so xUnit
/// does not discover their inherited facts when the opt-in connection is unavailable; each public aggregate
/// is skipped by <see cref="ConfigurationPostgreSqlFactAttribute"/> in that case.
/// </summary>
public class PostgreSqlConfigurationContractTests
{
	[ConfigurationPostgreSqlFact]
	[Trait("Category", "PostgreSql")]
	public Task ClientContracts() => ProviderFactRunner.RunAsync(new PostgreSqlClientContracts());

	[ConfigurationPostgreSqlFact]
	[Trait("Category", "PostgreSql")]
	public Task KeyContracts() => ProviderFactRunner.RunAsync(new PostgreSqlKeyContracts());

	[ConfigurationPostgreSqlFact]
	[Trait("Category", "PostgreSql")]
	public Task RealmContracts() => ProviderFactRunner.RunAsync(new PostgreSqlRealmContracts());

	[ConfigurationPostgreSqlFact]
	[Trait("Category", "PostgreSql")]
	public Task ResourceContracts() => ProviderFactRunner.RunAsync(new PostgreSqlResourceContracts());

	private sealed class PostgreSqlClientContracts : ClientStoreContractTests
	{
		protected override Task<StorageContractHarness> CreateHarnessAsync()
			=> PostgreSqlConfigurationStorageHarness.CreateAsync();
	}

	private sealed class PostgreSqlKeyContracts : KeyStoreContractTests
	{
		protected override Task<StorageContractHarness> CreateHarnessAsync()
			=> PostgreSqlConfigurationStorageHarness.CreateAsync();
	}

	private sealed class PostgreSqlRealmContracts : RealmStoreContractTests
	{
		protected override Task<StorageContractHarness> CreateHarnessAsync()
			=> PostgreSqlConfigurationStorageHarness.CreateAsync();
	}

	private sealed class PostgreSqlResourceContracts : ResourceStoreContractTests
	{
		protected override Task<StorageContractHarness> CreateHarnessAsync()
			=> PostgreSqlConfigurationStorageHarness.CreateAsync();
	}
}
