using Tests.Storage.Operational.Support;

namespace Tests.Storage.Operational;

/// <summary>
/// The acceptance of plan-replay-protection DF6 on the durable backing: concurrent presentations of the same
/// handle produce exactly one winner. This is the whole reason the contract is a single atomic operation — a
/// check followed by a write would let every caller here pass the check before any of them wrote.
/// <para>
/// Each caller has its own scope, its own <c>DbContext</c> and — pooling being off on a file-backed database —
/// its own connection, released together by an async start barrier. A shared in-memory fixture would funnel
/// everything through one connection and never contend at all.
/// </para>
/// </summary>
public class SqliteOperationalReplayProtectionConcurrencyTests
{
    private static readonly DateTimeOffset Expiration =
        new(Tests.Storage.Support.StorageContractHarness.Start.AddMinutes(15), TimeSpan.Zero);

    private const string Purpose = "PrivateKeyJwtSecretEvaluator";
    private const string Issuer = "client-race";

    [Theory]
    [InlineData(2)]
    [InlineData(8)]
    public async Task ConcurrentRegistrationsOfTheSameHandle_ProduceExactlyOneWinner(int callers)
    {
        await using var database = await SqliteOperationalFileDatabase.CreateMigratedAsync();
        var realm = SqliteOperationalFileDatabase.NewRealm();

        var results = new bool[callers];
        await SqliteOperationalFileDatabase.RunTogetherAsync(callers, async (index, ready, release) =>
        {
            await using var scope = database.CreateScope();
            var store = database.ReplayProtectionOf(scope);

            ready.SetResult();
            await release;

            results[index] = await store.TryAddAsync(
                realm.Id, Issuer, Purpose, "contended-jti", Expiration, default);
        });

        Assert.Single(results, added => added);
        Assert.Equal(1, await database.CountAsync("replay_handles"));
    }

    // Handles are independent: contention on one must not refuse another. Without this, a single winner could
    // also be achieved by something that simply refuses under load.
    [Fact]
    public async Task ConcurrentRegistrationsOfDifferentHandles_AllSucceed()
    {
        const int callers = 6;
        await using var database = await SqliteOperationalFileDatabase.CreateMigratedAsync();
        var realm = SqliteOperationalFileDatabase.NewRealm();

        var results = new bool[callers];
        await SqliteOperationalFileDatabase.RunTogetherAsync(callers, async (index, ready, release) =>
        {
            await using var scope = database.CreateScope();
            var store = database.ReplayProtectionOf(scope);

            ready.SetResult();
            await release;

            results[index] = await store.TryAddAsync(
                realm.Id, Issuer, Purpose, $"jti-{index}", Expiration, default);
        });

        Assert.All(results, added => Assert.True(added));
        Assert.Equal(callers, await database.CountAsync("replay_handles"));
    }

    // Two issuers presenting the same handle at the same time must both succeed: the losing side of a race here
    // would mean one client can deny another's authentication by guessing its jti (DF13).
    [Fact]
    public async Task ConcurrentRegistrationsBySeparateIssuers_AllSucceed()
    {
        const int callers = 6;
        await using var database = await SqliteOperationalFileDatabase.CreateMigratedAsync();
        var realm = SqliteOperationalFileDatabase.NewRealm();

        var results = new bool[callers];
        await SqliteOperationalFileDatabase.RunTogetherAsync(callers, async (index, ready, release) =>
        {
            await using var scope = database.CreateScope();
            var store = database.ReplayProtectionOf(scope);

            ready.SetResult();
            await release;

            results[index] = await store.TryAddAsync(
                realm.Id, $"client-{index}", Purpose, "shared-jti", Expiration, default);
        });

        Assert.All(results, added => Assert.True(added));
        Assert.Equal(callers, await database.CountAsync("replay_handles"));
    }
}
