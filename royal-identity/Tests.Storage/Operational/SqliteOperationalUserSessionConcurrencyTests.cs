using RoyalIdentity.Models;
using RoyalIdentity.Users;
using Tests.Storage.Operational.Support;

namespace Tests.Storage.Operational;

/// <summary>
/// The concurrency acceptances of SS-03/SS-04/SS-06 (plan Fase 3): recording the same client concurrently
/// leaves one row keeping the first sighting, and ending a session concurrently reports the transition to
/// exactly one caller.
/// <para>
/// Every writer has its own scope, its own <c>DbContext</c> and — pooling being off on a file-backed database —
/// its own connection, released together by an async start barrier. Simulated concurrency inside one
/// <c>DbContext</c> is explicitly not a valid acceptance.
/// </para>
/// </summary>
public class SqliteOperationalUserSessionConcurrencyTests
{
    private static readonly DateTime Start = Tests.Storage.Support.StorageContractHarness.Start;

    private const string SessionId = "session-race";

    private static UserSession NewSession(string sessionId = SessionId, string subjectId = "subject-race") => new()
    {
        Id = sessionId,
        SubjectId = subjectId,
        AuthenticationMethod = "pwd",
        IdentityProvider = "local",
        StartedAt = Start,
        LastSeenAt = Start,
        IsActive = true,
    };

    private static async Task SeedSessionAsync(
        SqliteOperationalFileDatabase database, Realm realm, UserSession session)
    {
        await using var scope = database.CreateScope();
        await database.StoresOf(scope).GetUserSessionStore(realm).CreateAsync(session);
    }

    private static async Task<UserSession?> ReadAsync(
        SqliteOperationalFileDatabase database, Realm realm, string sessionId = SessionId)
    {
        await using var scope = database.CreateScope();

        return await database.StoresOf(scope).GetUserSessionStore(realm).FindByIdAsync(sessionId);
    }

    // SS-03: concurrent records of the same client converge on a single row — the composite key is what
    // prevents the duplicate, and the loser refreshes the winner's entry instead of failing.
    [Fact]
    public async Task ConcurrentRecordsOfTheSameClient_LeaveExactlyOneRow()
    {
        await using var database = await SqliteOperationalFileDatabase.CreateMigratedAsync();
        var realm = SqliteOperationalFileDatabase.NewRealm();
        await SeedSessionAsync(database, realm, NewSession());

        await SqliteOperationalFileDatabase.RunTogetherAsync(8, async (_, ready, release) =>
        {
            await using var scope = database.CreateScope();
            var store = database.StoresOf(scope).GetUserSessionStore(realm);

            await store.FindByIdAsync(SessionId);
            ready.SetResult();
            await release;

            await store.RecordClientAsync(SessionId, "client-race");
        });

        Assert.Equal(1, await database.CountSessionClientsAsync());

        var session = await ReadAsync(database, realm);
        Assert.NotNull(session);
        var client = Assert.Single(session.Clients);
        Assert.Equal("client-race", client.ClientId);
        Assert.True(client.LastSeenAt >= client.FirstSeenAt);
    }

    // SS-03: whatever the race does to LastSeenAt, the first sighting is never rewritten.
    [Fact]
    public async Task ConcurrentRecords_PreserveTheFirstSeenAt_AndAdvanceTheLastSeenAt()
    {
        await using var database = await SqliteOperationalFileDatabase.CreateMigratedAsync();
        var realm = SqliteOperationalFileDatabase.NewRealm();
        await SeedSessionAsync(database, realm, NewSession());

        await using (var scope = database.CreateScope())
        {
            await database.StoresOf(scope).GetUserSessionStore(realm).RecordClientAsync(SessionId, "client-race");
        }

        var firstSeenAt = (await ReadAsync(database, realm))!.Clients.Single().FirstSeenAt;

        await SqliteOperationalFileDatabase.RunTogetherAsync(6, async (_, ready, release) =>
        {
            await using var scope = database.CreateScope();
            var store = database.StoresOf(scope).GetUserSessionStore(realm);

            await store.FindByIdAsync(SessionId);
            ready.SetResult();
            await release;

            await store.RecordClientAsync(SessionId, "client-race");
        });

        Assert.Equal(1, await database.CountSessionClientsAsync());

        var client = (await ReadAsync(database, realm))!.Clients.Single();
        Assert.Equal(firstSeenAt, client.FirstSeenAt);
        Assert.True(client.LastSeenAt >= firstSeenAt);
    }

    // SS-04/SS-06: ending is a conditional transition, so concurrent revocations of the same session add up to
    // exactly one effective end — the count is of transitions, not of attempts.
    [Fact]
    public async Task ConcurrentRevocations_ReportTheTransitionOnlyOnce()
    {
        await using var database = await SqliteOperationalFileDatabase.CreateMigratedAsync();
        var realm = SqliteOperationalFileDatabase.NewRealm();
        await SeedSessionAsync(database, realm, NewSession());

        var counts = new int[6];
        await SqliteOperationalFileDatabase.RunTogetherAsync(counts.Length, async (index, ready, release) =>
        {
            await using var scope = database.CreateScope();
            var store = database.StoresOf(scope).GetUserSessionStore(realm);

            await store.FindByIdAsync(SessionId);
            ready.SetResult();
            await release;

            counts[index] = await store.EndSessionsForSubjectAsync("subject-race", null);
        });

        Assert.Equal(1, counts.Sum());
        Assert.False((await ReadAsync(database, realm))!.IsActive);
    }

    // Concurrent records of different clients on the same session are independent: contention on one must not
    // lose the others.
    [Fact]
    public async Task ConcurrentRecordsOfDifferentClients_AllSurvive()
    {
        await using var database = await SqliteOperationalFileDatabase.CreateMigratedAsync();
        var realm = SqliteOperationalFileDatabase.NewRealm();
        await SeedSessionAsync(database, realm, NewSession());
        const int clients = 6;

        await SqliteOperationalFileDatabase.RunTogetherAsync(clients, async (index, ready, release) =>
        {
            await using var scope = database.CreateScope();
            var store = database.StoresOf(scope).GetUserSessionStore(realm);

            await store.FindByIdAsync(SessionId);
            ready.SetResult();
            await release;

            await store.RecordClientAsync(SessionId, $"client-{index}");
        });

        Assert.Equal(clients, await database.CountSessionClientsAsync());
        Assert.Equal(clients, (await ReadAsync(database, realm))!.Clients.Count);
    }
}
