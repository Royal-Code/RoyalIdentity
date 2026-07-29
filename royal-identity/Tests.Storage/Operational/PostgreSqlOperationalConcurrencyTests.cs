using System.Security.Claims;
using RoyalIdentity.Contracts.Storage;
using RoyalIdentity.Models;
using RoyalIdentity.Models.Scopes;
using RoyalIdentity.Models.Tokens;
using Tests.Storage.Configuration;
using Tests.Storage.Operational.Support;

namespace Tests.Storage.Operational;

/// <summary>
/// MP-2 and MP-3 against a real PostgreSQL 17 (plan Fase 7). Both are implemented with provider-neutral EF
/// primitives — a conditional delete/update whose affected-row count decides the winner — so the question this
/// suite answers is whether that still yields exactly one winner under an engine whose concurrency model is
/// nothing like SQLite's.
/// <para>
/// Every consumer has its own scope, its own <c>DbContext</c> and, pooling being off, its own connection;
/// they are released together by an async start barrier.
/// </para>
/// </summary>
public class PostgreSqlOperationalConcurrencyTests
{
    private static readonly DateTime Start = Tests.Storage.Support.StorageContractHarness.Start;

    private const string ClientId = "client-race";
    private const string RedirectUri = "https://client.contract.test/callback";
    private const string Handle = "rt-contended";

    // MP-2: N consumers, one code, exactly one gets it — and the code is gone afterwards.
    [StoragePostgreSqlFact]
    [Trait("Category", "PostgreSql")]
    public async Task ConcurrentCodeConsumers_ProduceExactlyOneSuccess()
    {
        await using var database = await PostgreSqlOperationalConcurrencyDatabase.CreateMigratedAsync();
        var realm = PostgreSqlOperationalConcurrencyDatabase.NewRealm();
        var code = await SeedCodeAsync(database, realm);
        const int consumers = 8;

        var results = new AuthorizationCode?[consumers];
        await SqliteOperationalFileDatabase.RunTogetherAsync(consumers, async (index, ready, release) =>
        {
            await using var scope = database.CreateScope();
            var store = database.StoresOf(scope).GetAuthorizationCodeStore(realm);

            // Everyone reads before anyone deletes: the window where a primitive ignoring the affected-row
            // count would hand the same code to more than one caller.
            await store.GetAuthorizationCodeAsync(code.Code, default);
            ready.SetResult();
            await release;

            results[index] = await store.ConsumeAuthorizationCodeAsync(code.Code, ClientId, RedirectUri, default);
        });

        var winner = Assert.Single(results, result => result is not null);
        Assert.Equal(code.Code, winner!.Code);
        Assert.Equal(0, await database.CountAsync("protocol_artifacts"));
    }

    // MP-3: N transitions from the same version — exactly one succeeds, the rest are conflicts, and the state
    // version advanced exactly once.
    [StoragePostgreSqlFact]
    [Trait("Category", "PostgreSql")]
    public async Task ConcurrentRefreshTransitions_ProduceExactlyOneSuccess()
    {
        await using var database = await PostgreSqlOperationalConcurrencyDatabase.CreateMigratedAsync();
        var realm = PostgreSqlOperationalConcurrencyDatabase.NewRealm();
        var seeded = await SeedRefreshTokenAsync(database, realm);
        const int consumers = 8;

        var outcomes = new RefreshTokenTransition[consumers];
        await SqliteOperationalFileDatabase.RunTogetherAsync(consumers, async (index, ready, release) =>
        {
            await using var scope = database.CreateScope();
            var store = database.StoresOf(scope).GetRefreshTokenStore(realm);

            ready.SetResult();
            await release;

            outcomes[index] = await store.TryConsumeAsync(
                Handle, seeded.StateVersion, Start.AddMinutes(1), default);
        });

        Assert.Single(outcomes, outcome => outcome.Outcome is RefreshTokenTransitionOutcome.Succeeded);
        Assert.All(
            outcomes.Where(outcome => outcome.Outcome is not RefreshTokenTransitionOutcome.Succeeded),
            outcome => Assert.Contains(
                outcome.Outcome,
                new[] { RefreshTokenTransitionOutcome.Conflict, RefreshTokenTransitionOutcome.AlreadyConsumed }));

        await using var reader = database.CreateScope();
        var current = await database.StoresOf(reader).GetRefreshTokenStore(realm).GetAsync(Handle, default);
        Assert.NotNull(current);
        Assert.Equal(seeded.StateVersion + 1, current.StateVersion);
        Assert.Equal(Start.AddMinutes(1), current.ConsumedTime);
    }

    // The create-only rule holds under contention too: two writers of the same handle produce one row, and the
    // loser fails instead of overwriting.
    [StoragePostgreSqlFact]
    [Trait("Category", "PostgreSql")]
    public async Task ConcurrentWritersOfTheSameHandle_ProduceOneRow()
    {
        await using var database = await PostgreSqlOperationalConcurrencyDatabase.CreateMigratedAsync();
        var realm = PostgreSqlOperationalConcurrencyDatabase.NewRealm();
        const int writers = 4;

        var failures = new Exception?[writers];
        await SqliteOperationalFileDatabase.RunTogetherAsync(writers, async (index, ready, release) =>
        {
            await using var scope = database.CreateScope();
            var store = database.StoresOf(scope).GetRefreshTokenStore(realm);

            ready.SetResult();
            await release;

            try
            {
                await store.StoreAsync(NewRefreshToken(realm, Handle), default);
            }
            catch (Exception exception)
            {
                failures[index] = exception;
            }
        });

        Assert.Equal(1, await database.CountAsync("protocol_artifacts"));
        Assert.Equal(writers - 1, failures.Count(failure => failure is not null));
    }

    private static AuthorizationCode NewCode(Realm realm)
    {
        var subject = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "subject-race")], "contract"));

        return new AuthorizationCode(
            ClientId, subject, "session-state", Start, 300, new RequestedResources(), RedirectUri)
        {
            RealmId = realm.Id,
        };
    }

    private static RefreshToken NewRefreshToken(Realm realm, string handle)
        => new("subject-race", "session-race", ["openid"], ClientId, "https://issuer.contract.test",
            Start, 3600, handle)
        {
            RealmId = realm.Id,
        };

    private static async Task<AuthorizationCode> SeedCodeAsync(
        PostgreSqlOperationalConcurrencyDatabase database, Realm realm)
    {
        var code = NewCode(realm);
        await using var scope = database.CreateScope();
        await database.StoresOf(scope).GetAuthorizationCodeStore(realm)
            .StoreAuthorizationCodeAsync(code, default);

        return code;
    }

    private static async Task<RefreshToken> SeedRefreshTokenAsync(
        PostgreSqlOperationalConcurrencyDatabase database, Realm realm)
    {
        await using var scope = database.CreateScope();
        var store = database.StoresOf(scope).GetRefreshTokenStore(realm);
        await store.StoreAsync(NewRefreshToken(realm, Handle), default);

        return (await store.GetAsync(Handle, default))!;
    }
}
