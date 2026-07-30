using Microsoft.Extensions.DependencyInjection;
using RoyalIdentity.Contracts.Defaults.ReplayProtection;
using RoyalIdentity.Contracts.Storage;
using RoyalIdentity.Extensions;

namespace Tests.Identity.ReplayProtection;

/// <summary>
/// Covers the declaration rule of plan-replay-protection DF12/DF14: a composition declares exactly one strategy,
/// and the startup validator refuses every other shape — in any environment, because
/// <c>WebApplication.CreateBuilder</c> only enables container validation in Development.
/// </summary>
public class ReplayProtectionCompositionTests
{
    [Fact]
    public void AddInMemoryReplayProtection_RegistersBothTheStoreAndItsDeclaration()
    {
        var services = new ServiceCollection().AddLogging();
        services.AddInMemoryReplayProtection();

        using var provider = services.BuildServiceProvider();

        var store = provider.GetRequiredService<IReplayProtectionStore>();
        var registration = Assert.Single(provider.GetServices<ReplayProtectionRegistration>());

        Assert.IsType<InMemoryReplayProtectionStore>(store);
        Assert.Equal(typeof(InMemoryReplayProtectionStore), registration.StoreType);
        Assert.Equal("AddInMemoryReplayProtection()", registration.ExtensionName);
    }

    [Fact]
    public async Task DeclaredStrategy_PassesStartup()
    {
        var services = new ServiceCollection().AddLogging();
        services.AddInMemoryReplayProtection();

        await using var provider = services.BuildServiceProvider();

        await new ReplayProtectionStartupValidator(provider).StartAsync(default);
    }

    [Fact]
    public async Task NoStrategyAndNoStore_FailsStartup()
    {
        await using var provider = new ServiceCollection().BuildServiceProvider();

        var error = await AssertRefusesAsync(provider);
        Assert.Contains("no replay-protection strategy was declared", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TwoDeclaredStrategies_FailStartup()
    {
        // Without this, the container's last-wins rule would quietly pick a strategy the operator did not choose.
        var services = new ServiceCollection().AddLogging();
        services.AddInMemoryReplayProtection();
        services.AddSingleton(new ReplayProtectionRegistration(
            "durable", "AddOperationalReplayProtection()", typeof(OtherStore)));

        await using var provider = services.BuildServiceProvider();

        var error = await AssertRefusesAsync(provider);
        Assert.Contains("more than one replay-protection strategy", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DeclarationWithoutStore_FailsStartup()
    {
        var services = new ServiceCollection();
        services.AddSingleton(new ReplayProtectionRegistration(
            "in-memory", "AddInMemoryReplayProtection()", typeof(InMemoryReplayProtectionStore)));

        await using var provider = services.BuildServiceProvider();

        var error = await AssertRefusesAsync(provider);
        Assert.Contains("no IReplayProtectionStore is registered", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task StoreWithoutDeclaration_FailsStartup()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IReplayProtectionStore, OtherStore>();

        await using var provider = services.BuildServiceProvider();

        var error = await AssertRefusesAsync(provider);
        Assert.Contains("without a strategy declaration", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DeclarationNotMatchingTheResolvedStore_FailsStartup()
    {
        // A later registration overriding the store is the realistic way a composition ends up protecting
        // itself with something other than what it declared.
        var services = new ServiceCollection().AddLogging();
        services.AddInMemoryReplayProtection();
        services.AddSingleton<IReplayProtectionStore, OtherStore>();

        await using var provider = services.BuildServiceProvider();

        var error = await AssertRefusesAsync(provider);
        Assert.Contains("but the container resolves", error.Message, StringComparison.Ordinal);
    }

    private static async Task<InvalidOperationException> AssertRefusesAsync(IServiceProvider provider)
    {
        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => new ReplayProtectionStartupValidator(provider).StartAsync(default));

        Assert.Contains("AddInMemoryReplayProtection()", error.Message, StringComparison.Ordinal);
        Assert.Contains("AddOperationalReplayProtection()", error.Message, StringComparison.Ordinal);

        return error;
    }

    private sealed class OtherStore : IReplayProtectionStore
    {
        public Task<bool> TryAddAsync(
            string realmId,
            string issuer,
            string purpose,
            string handle,
            DateTimeOffset expiration,
            CancellationToken ct) => Task.FromResult(true);
    }
}
