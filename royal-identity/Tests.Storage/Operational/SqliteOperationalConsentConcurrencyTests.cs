using RoyalIdentity.Models;
using Tests.Storage.Operational.Support;

namespace Tests.Storage.Operational;

/// <summary>
/// The concurrency acceptance of CN-01 (plan Fase 2): concurrent writers on the same
/// <c>(realm, subject, client)</c> never produce two rows, and one of them is the effective value.
/// <para>
/// Every writer here has its own scope, its own <c>DbContext</c> and — pooling being off on a file-backed
/// database — its own connection, released by a start barrier so they actually overlap. Simulated concurrency
/// inside one <c>DbContext</c> would prove nothing, and is explicitly not a valid acceptance.
/// </para>
/// </summary>
public class SqliteOperationalConsentConcurrencyTests
{
    private static readonly DateTime Start = Tests.Storage.Support.StorageContractHarness.Start;

    private const string Subject = "subject-race";
    private const string Client = "client-race";

    private static Consent NewConsent(Realm realm, string scope, DateTime creationTime)
    {
        var consent = new Consent
        {
            RealmId = realm.Id,
            SubjectId = Subject,
            ClientId = Client,
            CreationTime = creationTime,
            Expiration = null,
        };

        consent.AddScopes([new ConsentedScope { Scope = scope, CreationTime = creationTime }]);

        return consent;
    }

    private static async Task<Consent?> ReadAsync(SqliteOperationalFileDatabase database, Realm realm)
    {
        await using var scope = database.CreateScope();

        return await database.StoresOf(scope).GetUserConsentStore(realm)
            .GetUserConsentAsync(Subject, Client, default);
    }

    /// <summary>
    /// Writers that all target the same key, each in its own scope/context/connection, released together.
    /// </summary>
    private static Task WriteConcurrentlyAsync(
        SqliteOperationalFileDatabase database, Realm realm, int writers, Func<int, Consent> consentOf)
        => SqliteOperationalFileDatabase.RunTogetherAsync(writers, async (index, ready, release) =>
        {
            await using var scope = database.CreateScope();
            var store = database.StoresOf(scope).GetUserConsentStore(realm);

            // Warm up EF's first-use initialization before signalling, so the writers meet inside the upsert
            // rather than inside model building.
            await store.GetUserConsentAsync(Subject, Client, default);

            ready.SetResult();
            await release;

            await store.StoreUserConsentAsync(consentOf(index), default);
        });

    // Two writers, same key, released together. Both must complete, and exactly one row must remain holding
    // one of the two payloads whole — never a merge, never a duplicate.
    [Fact]
    public async Task TwoConcurrentWriters_OnTheSameKey_LeaveExactlyOneRow()
    {
        await using var database = await SqliteOperationalFileDatabase.CreateMigratedAsync();
        var realm = SqliteOperationalFileDatabase.NewRealm();

        await WriteConcurrentlyAsync(
            database, realm, 2, index => NewConsent(realm, $"scope-{index}", Start.AddMinutes(index + 1)));

        Assert.Equal(1, await database.CountConsentsAsync());

        var effective = await ReadAsync(database, realm);
        Assert.NotNull(effective);

        // One writer's payload survives whole; which one depends on the race, and either is correct.
        var scopes = effective.GetValidScopes();
        var winner = Assert.Single(scopes);
        Assert.Contains(winner, new[] { "scope-0", "scope-1" });
        Assert.Equal(
            winner == "scope-0" ? Start.AddMinutes(1) : Start.AddMinutes(2),
            effective.CreationTime);
    }

    // Scaled up: many writers, still one row. This is what would surface an insert path that swallowed the key
    // violation, or an upsert that lost the row entirely under contention.
    [Fact]
    public async Task ManyConcurrentWriters_OnTheSameKey_LeaveExactlyOneRow()
    {
        await using var database = await SqliteOperationalFileDatabase.CreateMigratedAsync();
        var realm = SqliteOperationalFileDatabase.NewRealm();

        await WriteConcurrentlyAsync(
            database, realm, 8, index => NewConsent(realm, $"scope-{index}", Start.AddMinutes(index)));

        Assert.Equal(1, await database.CountConsentsAsync());

        var effective = await ReadAsync(database, realm);
        Assert.NotNull(effective);
        Assert.Single(effective.GetValidScopes());
    }

    // A write that completes strictly after another has finished is unambiguously the effective one: this is
    // the deterministic half of "the last write wins", which a race alone cannot pin down.
    [Fact]
    public async Task AWriteCompletingAfterAnother_IsTheEffectiveOne()
    {
        await using var database = await SqliteOperationalFileDatabase.CreateMigratedAsync();
        var realm = SqliteOperationalFileDatabase.NewRealm();

        await using (var firstScope = database.CreateScope())
        {
            await database.StoresOf(firstScope).GetUserConsentStore(realm)
                .StoreUserConsentAsync(NewConsent(realm, "scope-first", Start), default);
        }

        // A different scope, a different DbContext and a different connection.
        await using (var secondScope = database.CreateScope())
        {
            await database.StoresOf(secondScope).GetUserConsentStore(realm)
                .StoreUserConsentAsync(NewConsent(realm, "scope-second", Start.AddMinutes(5)), default);
        }

        Assert.Equal(1, await database.CountConsentsAsync());

        var effective = await ReadAsync(database, realm);
        Assert.NotNull(effective);
        Assert.Equal(["scope-second"], effective.GetValidScopes());
        Assert.Equal(Start.AddMinutes(5), effective.CreationTime);
    }

    // Concurrent writers on different keys are independent: contention on one must not lose the others.
    [Fact]
    public async Task ConcurrentWriters_OnDifferentKeys_AllSurvive()
    {
        await using var database = await SqliteOperationalFileDatabase.CreateMigratedAsync();
        var realm = SqliteOperationalFileDatabase.NewRealm();
        const int writers = 6;

        await SqliteOperationalFileDatabase.RunTogetherAsync(writers, async (index, ready, release) =>
        {
            await using var scope = database.CreateScope();
            var store = database.StoresOf(scope).GetUserConsentStore(realm);
            var consent = new Consent
            {
                RealmId = realm.Id,
                SubjectId = $"subject-{index}",
                ClientId = Client,
                CreationTime = Start,
            };
            consent.AddScopes([new ConsentedScope { Scope = "openid", CreationTime = Start }]);

            await store.GetUserConsentAsync($"subject-{index}", Client, default);

            ready.SetResult();
            await release;

            await store.StoreUserConsentAsync(consent, default);
        });

        Assert.Equal(writers, await database.CountConsentsAsync());
    }
}
