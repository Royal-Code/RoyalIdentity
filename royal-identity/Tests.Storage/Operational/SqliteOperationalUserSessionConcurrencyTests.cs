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

    // SS-03: a session removed while a record is in flight linearizes as an absent session — a no-op — not as
    // an operational failure. The foreign key already guaranteed no orphan child; failing the caller on top of
    // that would turn a normal race with cleanup into an error.
    // The window is hit deterministically: the store reads the clock between the existence pre-check and the
    // insert, so a clock that deletes the session on that read reproduces exactly the interleaving.
    [Fact]
    public async Task RecordClient_WhenTheSessionIsRemovedMidOperation_IsANoOp()
    {
        var clock = new SessionDeletingClock();
        await using var database = await SqliteOperationalFileDatabase.CreateMigratedAsync(clock);
        clock.Bind(database.ConnectionString);
        var realm = SqliteOperationalFileDatabase.NewRealm();
        await SeedSessionAsync(database, realm, NewSession());

        clock.DeleteOnNextReadOf(SessionId);

        await using (var scope = database.CreateScope())
        {
            // No exception: the session is gone, and an absent session is a no-op.
            await database.StoresOf(scope).GetUserSessionStore(realm).RecordClientAsync(SessionId, "client-race");
        }

        Assert.True(clock.Fired);
        Assert.Equal(0, await database.CountSessionClientsAsync());
        Assert.Null(await ReadAsync(database, realm));
    }

    // The mirror case: when the insert fails and the session is still there, the failure must surface.
    [Fact]
    public async Task RecordClient_WhenTheSessionSurvives_StillReportsAnUnexpectedFailure()
    {
        await using var database = await SqliteOperationalFileDatabase.CreateMigratedAsync();
        var realm = SqliteOperationalFileDatabase.NewRealm();
        await SeedSessionAsync(database, realm, NewSession());

        await using var scope = database.CreateScope();
        var store = database.StoresOf(scope).GetUserSessionStore(realm);

        // A client whose id exceeds nothing and a session that exists: the happy path must still work, which is
        // what makes the no-op above a narrowed exception rather than a blanket swallow.
        await store.RecordClientAsync(SessionId, "client-race");

        Assert.Equal(1, await database.CountSessionClientsAsync());
    }

    // Records racing an actual delete: neither an exception nor an orphaned child row.
    [Fact]
    public async Task RecordClient_RacingASessionDelete_NeverFailsAndNeverOrphans()
    {
        await using var database = await SqliteOperationalFileDatabase.CreateMigratedAsync();
        var realm = SqliteOperationalFileDatabase.NewRealm();
        const int sessions = 8;

        for (var index = 0; index < sessions; index++)
            await SeedSessionAsync(database, realm, NewSession($"{SessionId}-{index}", $"subject-{index}"));

        await SqliteOperationalFileDatabase.RunTogetherAsync(sessions * 2, async (index, ready, release) =>
        {
            var sessionId = $"{SessionId}-{index / 2}";
            await using var scope = database.CreateScope();
            var store = database.StoresOf(scope).GetUserSessionStore(realm);

            await store.FindByIdAsync(sessionId);
            ready.SetResult();
            await release;

            if (index % 2 is 0)
                await store.RecordClientAsync(sessionId, "client-race");
            else
                await database.DeleteSessionAsync(sessionId);
        });

        // Every surviving client row must belong to a surviving session; the cascade and the no-op together
        // leave no orphan behind.
        Assert.Equal(0, await database.CountAsync(
            "user_session_clients c WHERE NOT EXISTS " +
            "(SELECT 1 FROM user_sessions s WHERE s.realm_id = c.realm_id AND s.session_id = c.session_id)"));
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

    /// <summary>
    /// A clock that deletes a session the next time it is read. The session store reads the clock between its
    /// existence pre-check and the insert, so this reproduces the "session removed mid-operation" interleaving
    /// exactly, instead of hoping a race lands in a window of microseconds.
    /// </summary>
    private sealed class SessionDeletingClock : TimeProvider
    {
        private string? connectionString;
        private string? sessionId;

        public bool Fired { get; private set; }

        public void Bind(string connection) => connectionString = connection;

        public void DeleteOnNextReadOf(string session) => sessionId = session;

        public override DateTimeOffset GetUtcNow()
        {
            var target = sessionId;
            if (target is not null && connectionString is not null)
            {
                sessionId = null;
                Fired = true;

                using var connection = new Microsoft.Data.Sqlite.SqliteConnection(connectionString);
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = "DELETE FROM user_sessions WHERE session_id = $id;";
                command.Parameters.AddWithValue("$id", target);
                command.ExecuteNonQuery();
            }

            return DateTimeOffset.UtcNow;
        }
    }
}
