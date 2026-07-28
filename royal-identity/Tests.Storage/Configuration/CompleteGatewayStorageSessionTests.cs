using Microsoft.Extensions.DependencyInjection;
using RoyalIdentity.Contracts.Storage;

namespace Tests.Storage.Configuration;

/// <summary>
/// The complete gateway lifecycle (Plan 4 Fase 7): a storage session owns a fresh scope combining the
/// Configuration and Operational EF contexts. The former Configuration-EF + fake-Operational composite was
/// transitional and is no longer part of the test graph.
/// </summary>
public class CompleteGatewayStorageSessionTests
{
    [Fact]
    public async Task Session_CombinesConfigurationAndOperationalEf_AndDisposalReleasesBoth()
    {
        await using var composition = await EntityFrameworkStorageGatewayTests.GatewayComposition.CreateAsync();
        var storageProvider = composition.Services.GetRequiredService<IStorageProvider>();

        var session = storageProvider.CreateSession();
        var storage = session.GetStorage();
        var realm = await composition.LoadRealmAsync(storage);
        var realms = storage.Realms;
        var sessions = storage.GetUserSessionStore(realm);

        Assert.NotNull(await realms.GetByIdAsync(realm.Id, default));
        Assert.Null(await sessions.FindByIdAsync("missing-session"));

        session.Dispose();

        await Assert.ThrowsAsync<ObjectDisposedException>(
            async () => await realms.GetByIdAsync(realm.Id, default));
        await Assert.ThrowsAsync<ObjectDisposedException>(
            async () => await sessions.FindByIdAsync("missing-session"));
        Assert.Throws<ObjectDisposedException>(session.GetStorage);
    }

    [Fact]
    public async Task EachSession_OwnsAnIndependentCompleteGatewayScope()
    {
        await using var composition = await EntityFrameworkStorageGatewayTests.GatewayComposition.CreateAsync();
        var storageProvider = composition.Services.GetRequiredService<IStorageProvider>();

        using var first = storageProvider.CreateSession();
        using var second = storageProvider.CreateSession();

        Assert.NotSame(first, second);
        Assert.NotSame(first.GetStorage(), second.GetStorage());
    }
}
