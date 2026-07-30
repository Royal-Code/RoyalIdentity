using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RoyalIdentity.Contracts.Storage;
using RoyalIdentity.Data.Operational.Entities;
using RoyalIdentity.Storage.EntityFramework.Operational.Maintenance;
using RoyalIdentity.Storage.EntityFramework.Operational.Stores;
using Tests.Storage.Operational.Support;

namespace Tests.Storage.Operational;

/// <summary>
/// The durable replay-protection backing over <c>replay_handles</c> (matrix RC-01/RC-02,
/// plan-replay-protection Fase 2), asserted identically on every provider. Concurrency is not here: proving a
/// single winner needs genuinely independent connections, which these shared fixtures cannot give.
/// </summary>
public abstract class OperationalReplayProtectionTests : OperationalParitySuite
{
    private static readonly DateTimeOffset Expiration =
        new(Tests.Storage.Support.StorageContractHarness.Start.AddMinutes(15), TimeSpan.Zero);

    private const string Purpose = "PrivateKeyJwtSecretEvaluator";
    private const string Issuer = "client-a";

    private static IReplayProtectionStore Store(IOperationalParityHarness harness)
        => harness.ScopedServices.GetRequiredService<IReplayProtectionStore>();

    private static async Task<List<ReplayHandleEntity>> HandlesAsync(IOperationalParityHarness harness)
    {
        await using var context = harness.NewOperationalContext();
        return await context.Set<ReplayHandleEntity>().AsNoTracking().ToListAsync();
    }

    [Fact]
    public async Task FirstRegistration_Succeeds_AndTheSecondAnswersReplay()
    {
        await using var harness = await CreateHarnessAsync();
        var store = Store(harness);

        Assert.True(await store.TryAddAsync(
            harness.RealmA.Id, Issuer, Purpose, "jti-1", Expiration, default));
        Assert.False(await store.TryAddAsync(
            harness.RealmA.Id, Issuer, Purpose, "jti-1", Expiration, default));

        Assert.Single(await HandlesAsync(harness));
    }

    // DF13: the identity has four dimensions and each one of them separates. A single-column key would have made
    // one client able to occupy another's handle, and one realm another realm's.
    [Fact]
    public async Task RecordsOfDifferentIdentities_DoNotInterfere()
    {
        await using var harness = await CreateHarnessAsync();
        var store = Store(harness);
        var realmA = harness.RealmA.Id;

        Assert.True(await store.TryAddAsync(realmA, Issuer, Purpose, "jti-1", Expiration, default));

        Assert.True(await store.TryAddAsync(
            harness.RealmB.Id, Issuer, Purpose, "jti-1", Expiration, default));
        Assert.True(await store.TryAddAsync(realmA, "client-b", Purpose, "jti-1", Expiration, default));
        Assert.True(await store.TryAddAsync(realmA, Issuer, "OtherPurpose", "jti-1", Expiration, default));
        Assert.True(await store.TryAddAsync(realmA, Issuer, Purpose, "jti-2", Expiration, default));

        Assert.Equal(5, (await HandlesAsync(harness)).Count);
    }

    // DF8: the write path never compares expiration, so a row kept past its expiration still answers replay.
    // Losing this would make the protection depend on how promptly cleanup runs.
    [Fact]
    public async Task ExpiredButNotYetCleanedRecord_StillAnswersReplay()
    {
        await using var harness = await CreateHarnessAsync();
        var store = Store(harness);

        Assert.True(await store.TryAddAsync(
            harness.RealmA.Id, Issuer, Purpose, "jti-1", Expiration, default));

        harness.Clock.Now = Expiration.AddDays(1);

        Assert.False(await store.TryAddAsync(
            harness.RealmA.Id, Issuer, Purpose, "jti-1", Expiration.AddDays(2), default));
    }

    // DF4: the handle is a client-chosen jti. It must not be readable back out of the database.
    [Fact]
    public async Task TheRawHandle_IsNeverPersisted()
    {
        const string handle = "jti-that-must-not-appear";
        await using var harness = await CreateHarnessAsync();

        await Store(harness).TryAddAsync(harness.RealmA.Id, Issuer, Purpose, handle, Expiration, default);

        var row = Assert.Single(await HandlesAsync(harness));
        Assert.DoesNotContain(handle, row.HandleDigest, StringComparison.OrdinalIgnoreCase);
        Assert.NotEqual(handle, row.Issuer, StringComparer.Ordinal);
        Assert.NotEqual(handle, row.Purpose, StringComparer.Ordinal);
        Assert.NotEqual(handle, row.RealmId, StringComparer.Ordinal);
    }

    // The row is realm-, issuer- and purpose-addressed in clear, and only the handle is digested: those three are
    // columns of their own precisely so cleanup and purge can filter on them (DF4/DF17).
    [Fact]
    public async Task TheRow_KeepsRealmIssuerAndPurposeQueryable()
    {
        await using var harness = await CreateHarnessAsync();

        await Store(harness).TryAddAsync(harness.RealmA.Id, Issuer, Purpose, "jti-1", Expiration, default);

        var row = Assert.Single(await HandlesAsync(harness));
        Assert.Equal(harness.RealmA.Id, row.RealmId);
        Assert.Equal(Issuer, row.Issuer);
        Assert.Equal(Purpose, row.Purpose);
        Assert.Equal(Expiration.UtcDateTime, row.ExpiresAtUtc);
    }

    // The boundary matters more here than for any other lifecycle: a handle's expiration is the artifact's own
    // expiration plus the tolerated clock skew, and the artifact is still acceptable at that exact instant. An
    // inclusive sweep would drop the protection while the assertion it protects is still valid.
    [Fact]
    public async Task Cleanup_RemovesHandlesStrictlyPastTheirExpiration()
    {
        await using var harness = await CreateHarnessAsync();
        var store = Store(harness);
        var realmA = harness.RealmA.Id;

        await store.TryAddAsync(realmA, Issuer, Purpose, "at-the-boundary", Expiration, default);
        await store.TryAddAsync(realmA, Issuer, Purpose, "past-the-boundary", Expiration.AddSeconds(-1), default);

        var maintenance = harness.ScopedServices.GetRequiredService<IOperationalMaintenance>();
        var report = await maintenance.CleanupAsync(Expiration.UtcDateTime, 100);

        Assert.Equal(1, report.ReplayHandles);
        Assert.Equal(1, report.Total);

        // And the surviving one is the boundary row, which is still protecting something.
        var remaining = Assert.Single(await HandlesAsync(harness));
        Assert.Equal(Expiration.UtcDateTime, remaining.ExpiresAtUtc);
    }

    [Fact]
    public async Task Cleanup_IsBoundedByTheBatchSize()
    {
        await using var harness = await CreateHarnessAsync();
        var store = Store(harness);
        var realmA = harness.RealmA.Id;

        for (var index = 0; index < 5; index++)
        {
            await store.TryAddAsync(
                realmA, Issuer, Purpose, $"jti-{index}", Expiration.AddSeconds(-1), default);
        }

        var maintenance = harness.ScopedServices.GetRequiredService<IOperationalMaintenance>();

        Assert.Equal(2, (await maintenance.CleanupAsync(Expiration.UtcDateTime, 2)).ReplayHandles);
        Assert.Equal(3, (await HandlesAsync(harness)).Count);
    }

    [Fact]
    public async Task Purge_RemovesTheRealmsHandles_AndLeavesTheOtherRealmAlone()
    {
        await using var harness = await CreateHarnessAsync();
        var store = Store(harness);

        await store.TryAddAsync(harness.RealmA.Id, Issuer, Purpose, "jti-1", Expiration, default);
        await store.TryAddAsync(harness.RealmA.Id, Issuer, Purpose, "jti-2", Expiration, default);
        await store.TryAddAsync(harness.RealmB.Id, Issuer, Purpose, "jti-1", Expiration, default);

        var maintenance = harness.ScopedServices.GetRequiredService<IOperationalMaintenance>();
        var report = await maintenance.PurgeRealmAsync(harness.RealmA.Id);

        Assert.Equal(2, report.ReplayHandles);

        var remaining = Assert.Single(await HandlesAsync(harness));
        Assert.Equal(harness.RealmB.Id, remaining.RealmId);
    }

    [Theory]
    [InlineData("", Issuer, Purpose, "jti")]
    [InlineData("realm", "", Purpose, "jti")]
    [InlineData("realm", Issuer, "", "jti")]
    [InlineData("realm", Issuer, Purpose, "")]
    public async Task IncompleteIdentity_IsRejected(
        string realmId, string issuer, string purpose, string handle)
    {
        await using var harness = await CreateHarnessAsync();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            Store(harness).TryAddAsync(realmId, issuer, purpose, handle, Expiration, default));
    }

    /// <summary>SQLite runs this suite unconditionally; it is the baseline the other provider must match.</summary>
    public sealed class Sqlite : OperationalReplayProtectionTests
    {
        private protected override Task<IOperationalParityHarness> CreateHarnessAsync(
            IAuthorizeParametersHandleGenerator? handleGenerator = null,
            Action<OperationalCleanupOptions>? cleanup = null)
            => SqliteParityHarness.CreateAsync(handleGenerator, cleanup);
    }
}

/// <summary>
/// The same suite over PostgreSQL. The concrete suite stays private so xUnit does not discover its scenarios
/// when the opt-in connection is unavailable.
/// </summary>
public class PostgreSqlReplayProtectionTests
{
    [Tests.Storage.Configuration.StoragePostgreSqlFact]
    [Trait("Category", "PostgreSql")]
    public Task ReplayProtection()
        => Tests.Storage.Configuration.Support.ProviderFactRunner.RunAsync(new PostgreSqlSuite());

    private sealed class PostgreSqlSuite : OperationalReplayProtectionTests
    {
        private protected override Task<IOperationalParityHarness> CreateHarnessAsync(
            IAuthorizeParametersHandleGenerator? handleGenerator = null,
            Action<OperationalCleanupOptions>? cleanup = null)
            => PostgreSqlParityHarness.CreateAsync(handleGenerator, cleanup);
    }
}
