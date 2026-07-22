using Tests.Storage.Configuration.Support;
using Tests.Storage.Support;

namespace Tests.Storage.Contracts;

/// <summary>
/// Contract of <c>IClientStore</c> (matrix CL-01/CL-02): realm-bound configuration reads. The any/enabled
/// distinction is expressed by the API itself; the enabled filter is a security rule (`preservar`).
/// </summary>
public abstract class ClientStoreContractTests : StorageContractTests
{
	// CL-01: the plain lookup returns the client even when disabled (the API expresses the distinction).
	[Fact]
	public async Task FindClientById_ReturnsSeededClient_EvenWhenDisabled()
	{
		await using var harness = await CreateHarnessAsync();
		await harness.SeedClientAsync(NewClient(harness.RealmA, "contract-disabled", enabled: false));

		var client = await harness.Storage.GetClientStore(harness.RealmA)
			.FindClientByIdAsync("contract-disabled", default);

		Assert.NotNull(client);
		Assert.False(client.Enabled);
	}

	// CL-02 `preservar`: security consumers (endpoints, CORS, secret evaluators) must never see a disabled client.
	[Fact]
	public async Task FindEnabledClientById_FiltersOutDisabledClient()
	{
		await using var harness = await CreateHarnessAsync();
		await harness.SeedClientAsync(NewClient(harness.RealmA, "contract-disabled", enabled: false));

		var client = await harness.Storage.GetClientStore(harness.RealmA)
			.FindEnabledClientByIdAsync("contract-disabled", default);

		Assert.Null(client);
	}

	// CL-02: an enabled client is returned by the enabled lookup.
	[Fact]
	public async Task FindEnabledClientById_ReturnsEnabledClient()
	{
		await using var harness = await CreateHarnessAsync();
		await harness.SeedClientAsync(NewClient(harness.RealmA, "contract-enabled"));

		var client = await harness.Storage.GetClientStore(harness.RealmA)
			.FindEnabledClientByIdAsync("contract-enabled", default);

		Assert.NotNull(client);
		Assert.Equal("contract-enabled", client.Id);
	}

	// CL-01 (Fase 5/DF25 closed): absent lookup returns null. Load-bearing for LoadClient/token
	// validation error paths.
	[Fact]
	public async Task FindClientById_UnknownClient_ReturnsNull()
	{
		await using var harness = await CreateHarnessAsync();

		var client = await harness.Storage.GetClientStore(harness.RealmA)
			.FindClientByIdAsync("contract-unknown-client", default);

		Assert.Null(client);
	}

	// DF18 (Fase 5): client_id comparison is Ordinal — an id differing only by casing is another client
	// (never provider collation; parity between SQLite and PostgreSQL).
	[Fact]
	public async Task FindClientById_DifferingOnlyByCase_ReturnsNull()
	{
		await using var harness = await CreateHarnessAsync();
		await harness.SeedClientAsync(NewClient(harness.RealmA, "contract-case-client"));

		var client = await harness.Storage.GetClientStore(harness.RealmA)
			.FindClientByIdAsync("CONTRACT-CASE-CLIENT", default);

		Assert.Null(client);
	}

	// DF6: a client exists only in the realm it was seeded into.
	[Fact]
	public async Task ClientSeededInOneRealm_IsNotVisibleInAnotherRealm()
	{
		await using var harness = await CreateHarnessAsync();
		await harness.SeedClientAsync(NewClient(harness.RealmA, "contract-only-a"));

		var inB = await harness.Storage.GetClientStore(harness.RealmB)
			.FindClientByIdAsync("contract-only-a", default);

		Assert.Null(inB);
	}

	// DF6: the same client id in two realms resolves to each realm's own registration.
	[Fact]
	public async Task SameClientId_InTwoRealms_ResolvesToEachRealmsOwnClient()
	{
		await using var harness = await CreateHarnessAsync();
		await harness.SeedClientAsync(NewClient(harness.RealmA, "contract-shared-id", name: "Client of realm A"));
		await harness.SeedClientAsync(NewClient(harness.RealmB, "contract-shared-id", name: "Client of realm B"));

		var inA = await harness.Storage.GetClientStore(harness.RealmA)
			.FindClientByIdAsync("contract-shared-id", default);
		var inB = await harness.Storage.GetClientStore(harness.RealmB)
			.FindClientByIdAsync("contract-shared-id", default);

		Assert.NotNull(inA);
		Assert.NotNull(inB);
		Assert.Equal("Client of realm A", inA.Name);
		Assert.Equal("Client of realm B", inB.Name);
	}

	public sealed class InMemory : ClientStoreContractTests
	{
		protected override Task<StorageContractHarness> CreateHarnessAsync() => InMemoryStorageHarness.CreateAsync();
	}

	public sealed class Sqlite : ClientStoreContractTests
	{
		protected override Task<StorageContractHarness> CreateHarnessAsync()
			=> SqliteConfigurationStorageHarness.CreateAsync();
	}
}
