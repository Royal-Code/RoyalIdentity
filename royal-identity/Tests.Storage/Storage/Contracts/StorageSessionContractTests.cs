using Microsoft.Extensions.DependencyInjection;
using RoyalIdentity.Contracts.Storage;

namespace Tests.Storage.Contracts;

/// <summary>
/// Contract of <c>IStorageProvider</c>/<c>IStorageSession</c> (matrix SP-01..SP-03): a session is a lifetime
/// seam giving access to a usable <c>IStorage</c> until disposed — not a Unit of Work (DF21). The concrete
/// variant uses the complete EF gateway, including both Configuration and Operational contexts.
/// </summary>
public abstract class StorageSessionContractTests
{
    // SP-01/SP-02 `preservar` (DF21): within its lifetime, the session provides storage access able to
    // read configuration (the key cache depends on this exact usage).
    [Fact]
    public async Task CreateSession_ProvidesUsableStorage_WithinItsLifetime()
    {
        await using var harness = await CreateHarnessAsync();

        using var session = harness.Provider.CreateSession();
        var storage = session.GetStorage();
        var realm = await storage.Realms.GetByIdAsync(EntityFrameworkStorageGatewayTests.GatewayComposition.RealmAId, default);

        Assert.NotNull(realm);
        Assert.Equal(EntityFrameworkStorageGatewayTests.GatewayComposition.RealmAId, realm.Id);
    }

    // SP-03 `preservar` (DF21): disposal completes without error; sessions are independently creatable.
    [Fact]
    public async Task Sessions_AreIndependentlyCreatableAndDisposable()
    {
        await using var harness = await CreateHarnessAsync();

        var first = harness.Provider.CreateSession();
        first.Dispose();

        using var second = harness.Provider.CreateSession();
        var realm = await second.GetStorage().Realms.GetByIdAsync(
            EntityFrameworkStorageGatewayTests.GatewayComposition.RealmBId,
            default);

        Assert.NotNull(realm);
    }

    protected abstract Task<StorageSessionHarness> CreateHarnessAsync();

    protected sealed class StorageSessionHarness(
        EntityFrameworkStorageGatewayTests.GatewayComposition composition) : IAsyncDisposable
    {
        public IStorageProvider Provider => composition.Services.GetRequiredService<IStorageProvider>();

        public ValueTask DisposeAsync() => composition.DisposeAsync();
    }

    public sealed class SqliteGateway : StorageSessionContractTests
    {
        protected override async Task<StorageSessionHarness> CreateHarnessAsync()
            => new(await EntityFrameworkStorageGatewayTests.GatewayComposition.CreateAsync());
    }
}
