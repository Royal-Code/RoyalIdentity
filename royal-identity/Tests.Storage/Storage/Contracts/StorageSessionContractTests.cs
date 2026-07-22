using Tests.Storage.Support;

namespace Tests.Storage.Contracts;

/// <summary>
/// Contract of <c>IStorageProvider</c>/<c>IStorageSession</c> (matrix SP-01..SP-03): a session is a lifetime
/// seam giving access to a usable <c>IStorage</c> until disposed — not a Unit of Work (DF21). No behavior is
/// asserted after disposal: the fake's no-op <c>Dispose</c> is `descartar` and the EF adapter will dispose real
/// resources (Plano 2 acceptance for the adapter implementation).
/// </summary>
public abstract class StorageSessionContractTests : StorageContractTests
{
	// SP-01/SP-02 `preservar` (DF21): within its lifetime, the session provides storage access able to
	// read configuration (the key cache depends on this exact usage).
	[Fact]
	public async Task CreateSession_ProvidesUsableStorage_WithinItsLifetime()
	{
		await using var harness = await CreateHarnessAsync();

		using var session = harness.Provider.CreateSession();
		var storage = session.GetStorage();
		var realm = await storage.Realms.GetByIdAsync(harness.RealmA.Id, default);

		Assert.NotNull(realm);
		Assert.Equal(harness.RealmA.Id, realm.Id);
	}

	// SP-03 `preservar` (DF21): disposal completes without error; sessions are independently creatable.
	[Fact]
	public async Task Sessions_AreIndependentlyCreatableAndDisposable()
	{
		await using var harness = await CreateHarnessAsync();

		var first = harness.Provider.CreateSession();
		first.Dispose();

		using var second = harness.Provider.CreateSession();
		var realm = await second.GetStorage().Realms.GetByIdAsync(harness.RealmB.Id, default);

		Assert.NotNull(realm);
	}

	public sealed class InMemory : StorageSessionContractTests
	{
		protected override Task<StorageContractHarness> CreateHarnessAsync() => InMemoryStorageHarness.CreateAsync();
	}
}
