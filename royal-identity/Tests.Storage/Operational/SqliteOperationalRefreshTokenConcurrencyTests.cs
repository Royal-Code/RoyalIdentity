using RoyalIdentity.Contracts.Storage;
using RoyalIdentity.Models;
using RoyalIdentity.Models.Tokens;
using Tests.Storage.Operational.Support;

namespace Tests.Storage.Operational;

/// <summary>
/// The conditional-transition acceptance of MP-3 (plan Fase 5, DF12): concurrent renewals of the same refresh
/// token produce exactly one initial <c>null → ConsumedTime</c> transition, and a loser is never told it won.
/// <para>
/// Every caller has its own scope, its own <c>DbContext</c> and — pooling being off on a file-backed database —
/// its own connection. <see cref="ReadBeforeWriteInterceptor"/> additionally pins the interleaving that matters:
/// everyone materializes the token before anyone tries to move it, which is precisely the window a trivial CAS
/// would sail through.
/// </para>
/// </summary>
public class SqliteOperationalRefreshTokenConcurrencyTests
{
    private static readonly DateTime Start = Tests.Storage.Support.StorageContractHarness.Start;

    private const string Handle = "rt-race";

    private static RefreshToken NewToken(Realm realm)
        => new("subject-race", "session-race", ["openid"], "client-race", "https://issuer.contract.test",
            Start, 3600, Handle)
        {
            RealmId = realm.Id,
        };

    private static async Task SeedAsync(SqliteOperationalFileDatabase database, Realm realm)
    {
        await using var scope = database.CreateScope();
        await database.StoresOf(scope).GetRefreshTokenStore(realm).StoreAsync(NewToken(realm), default);
    }

    // The acceptance: N renewals, one token, exactly one initial transition.
    [Theory]
    [InlineData(2)]
    [InlineData(8)]
    public async Task ConcurrentTransitions_ProduceExactlyOneSuccess(int callers)
    {
        await using var database = await SqliteOperationalFileDatabase.CreateMigratedAsync();
        var realm = SqliteOperationalFileDatabase.NewRealm();
        await SeedAsync(database, realm);

        var outcomes = new RefreshTokenTransitionOutcome[callers];
        await SqliteOperationalFileDatabase.RunTogetherAsync(callers, async (index, ready, release) =>
        {
            await using var scope = database.CreateScope();
            var store = database.StoresOf(scope).GetRefreshTokenStore(realm);

            // Each caller presents the version it materialized — never a value derived from what it will write.
            var materialized = await store.GetAsync(Handle, default);
            ready.SetResult();
            await release;

            outcomes[index] = (await store.TryConsumeAsync(
                Handle, materialized!.StateVersion, Start.AddMinutes(1), default)).Outcome;
        });

        Assert.Single(outcomes, outcome => outcome is RefreshTokenTransitionOutcome.Succeeded);
        Assert.All(
            outcomes.Where(outcome => outcome is not RefreshTokenTransitionOutcome.Succeeded),
            outcome => Assert.True(
                outcome is RefreshTokenTransitionOutcome.AlreadyConsumed or RefreshTokenTransitionOutcome.Conflict,
                $"a loser reported {outcome}"));
    }

    // The same acceptance with the interleaving pinned: everyone reads before anyone writes, which is the shape
    // a CAS that compared an instance against itself would pass.
    [Fact]
    public async Task ConcurrentTransitions_AllReadingBeforeAnyWrites_StillProduceExactlyOneSuccess()
    {
        const int callers = 4;
        var interceptor = new ReadBeforeWriteInterceptor(callers);
        await using var database = await SqliteOperationalFileDatabase.CreateMigratedAsync(interceptor: interceptor);
        var realm = SqliteOperationalFileDatabase.NewRealm();
        await SeedAsync(database, realm);

        var outcomes = new RefreshTokenTransitionOutcome[callers];
        var versions = new int[callers];

        await SqliteOperationalFileDatabase.RunTogetherAsync(callers, async (index, ready, release) =>
        {
            await using var scope = database.CreateScope();
            var store = database.StoresOf(scope).GetRefreshTokenStore(realm);

            ready.SetResult();
            await release;

            interceptor.Arm();
            var materialized = await store.GetAsync(Handle, default);
            versions[index] = materialized!.StateVersion;

            outcomes[index] = (await store.TryConsumeAsync(
                Handle, materialized.StateVersion, Start.AddMinutes(1), default)).Outcome;
        });

        Assert.True(interceptor.Interleaved, "every caller must have materialized before any of them moved the token");
        // They all held the same version, so only the conditional update itself can separate them.
        Assert.Single(versions.Distinct());
        Assert.Single(outcomes, outcome => outcome is RefreshTokenTransitionOutcome.Succeeded);
    }

    // Only the winner marks the token; the consumption timestamp is the winner's, not the last writer's.
    [Fact]
    public async Task ConcurrentTransitions_LeaveTheWinnersConsumptionTimestamp()
    {
        await using var database = await SqliteOperationalFileDatabase.CreateMigratedAsync();
        var realm = SqliteOperationalFileDatabase.NewRealm();
        await SeedAsync(database, realm);
        const int callers = 6;

        var consumedAt = new DateTime[callers];
        var outcomes = new RefreshTokenTransitionOutcome[callers];

        await SqliteOperationalFileDatabase.RunTogetherAsync(callers, async (index, ready, release) =>
        {
            await using var scope = database.CreateScope();
            var store = database.StoresOf(scope).GetRefreshTokenStore(realm);

            var materialized = await store.GetAsync(Handle, default);
            consumedAt[index] = Start.AddMinutes(index + 1);
            ready.SetResult();
            await release;

            outcomes[index] = (await store.TryConsumeAsync(
                Handle, materialized!.StateVersion, consumedAt[index], default)).Outcome;
        });

        var winner = Array.IndexOf(outcomes, RefreshTokenTransitionOutcome.Succeeded);
        Assert.NotEqual(-1, winner);

        await using var readScope = database.CreateScope();
        var persisted = await database.StoresOf(readScope).GetRefreshTokenStore(realm).GetAsync(Handle, default);

        Assert.Equal(consumedAt[winner], persisted!.ConsumedTime);
    }

    // MP-3 belongs directly to the base store contract returned by the EF factory (DF46).
    [Fact]
    public async Task TheEfRefreshTokenStore_ExposesConditionalTransitionsThroughTheBaseContract()
    {
        await using var database = await SqliteOperationalFileDatabase.CreateMigratedAsync();
        var realm = SqliteOperationalFileDatabase.NewRealm();

        await using var scope = database.CreateScope();
        var store = database.StoresOf(scope).GetRefreshTokenStore(realm);

        Assert.IsAssignableFrom<IRefreshTokenStore>(store);
    }
}
