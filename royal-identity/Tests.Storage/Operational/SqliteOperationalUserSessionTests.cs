using Microsoft.EntityFrameworkCore;
using RoyalIdentity.Data.Operational.Entities;
using RoyalIdentity.Models;
using RoyalIdentity.Users;
using Tests.Storage.Operational.Support;

namespace Tests.Storage.Operational;

/// <summary>
/// Acceptances only the EF provider can satisfy for SSO sessions (plan Fase 3, DF15/DF17): create-only
/// rejection, the full graph surviving the round-trip, the terminal timestamp that cleanup depends on, and the
/// conditional operations that keep record-client, touch and end from losing each other's change. The shared
/// semantics live in the provider-neutral <c>UserSessionStoreContractTests</c>.
/// </summary>
public class SqliteOperationalUserSessionTests
{
    private static readonly DateTime Start = Tests.Storage.Support.StorageContractHarness.Start;

    private static UserSession NewSession(
        string sessionId,
        string subjectId = "subject-a",
        bool isActive = true,
        DateTime? expiresAt = null,
        string? securityStamp = "stamp-1") => new()
        {
            Id = sessionId,
            SubjectId = subjectId,
            AuthenticationMethod = "pwd",
            IdentityProvider = "local",
            StartedAt = Start,
            LastSeenAt = Start,
            IsActive = isActive,
            ExpiresAt = expiresAt,
            SecurityStamp = securityStamp,
        };

    private static async Task<List<UserSessionEntity>> SessionRowsAsync(SqliteOperationalStorageHarness harness)
    {
        await using var context = harness.NewOperationalContext();

        return await context.Set<UserSessionEntity>().AsNoTracking().ToListAsync();
    }

    private static async Task<List<UserSessionClientEntity>> ClientRowsAsync(SqliteOperationalStorageHarness harness)
    {
        await using var context = harness.NewOperationalContext();

        return await context.Set<UserSessionClientEntity>().AsNoTracking().ToListAsync();
    }

    // SS-01: create-only. The primary key is the authority, so a duplicate sid in the same realm fails visibly
    // instead of replacing a live session.
    [Fact]
    public async Task Create_DuplicateSidInTheSameRealm_FailsWithoutOverwriting()
    {
        await using var harness = await SqliteOperationalStorageHarness.CreateConcreteAsync();
        var store = harness.Storage.GetUserSessionStore(harness.RealmA);
        await store.CreateAsync(NewSession("dup-sid", "subject-first"));

        await Assert.ThrowsAnyAsync<DbUpdateException>(
            async () => await store.CreateAsync(NewSession("dup-sid", "subject-second")));

        var found = await store.FindByIdAsync("dup-sid");
        Assert.NotNull(found);
        Assert.Equal("subject-first", found.SubjectId);
    }

    // SS-01: the whole graph is persisted, including the clients the session already carried.
    [Fact]
    public async Task Create_PersistsTheCompleteGraph()
    {
        await using var harness = await SqliteOperationalStorageHarness.CreateConcreteAsync();
        var session = NewSession("graph-sid", expiresAt: Start.AddHours(8), securityStamp: "stamp-graph");
        session.Clients.Add(new UserSessionClient("client-a", Start, Start.AddMinutes(1)));
        session.Clients.Add(new UserSessionClient("client-b", Start.AddMinutes(2), Start.AddMinutes(3)));

        await harness.Storage.GetUserSessionStore(harness.RealmA).CreateAsync(session);
        var found = await harness.Storage.GetUserSessionStore(harness.RealmA).FindByIdAsync("graph-sid");

        Assert.NotNull(found);
        Assert.Equal("subject-a", found.SubjectId);
        Assert.Equal("pwd", found.AuthenticationMethod);
        Assert.Equal("local", found.IdentityProvider);
        Assert.Equal(Start, found.StartedAt);
        Assert.Equal(Start, found.LastSeenAt);
        Assert.Equal(Start.AddHours(8), found.ExpiresAt);
        Assert.Equal("stamp-graph", found.SecurityStamp);
        Assert.True(found.IsActive);
        Assert.Equal(2, found.Clients.Count);
        Assert.Contains(found.Clients, client =>
            client.ClientId == "client-a" && client.FirstSeenAt == Start && client.LastSeenAt == Start.AddMinutes(1));
        Assert.Contains(found.Clients, client =>
            client.ClientId == "client-b"
            && client.FirstSeenAt == Start.AddMinutes(2)
            && client.LastSeenAt == Start.AddMinutes(3));
    }

    // DF17: ending records the terminal instant cleanup keys on, and a repeat preserves the first one.
    [Fact]
    public async Task End_RecordsTheTerminalTimestamp_AndARepeatPreservesTheFirst()
    {
        await using var harness = await SqliteOperationalStorageHarness.CreateConcreteAsync();
        var store = harness.Storage.GetUserSessionStore(harness.RealmA);
        await store.CreateAsync(NewSession("ended-sid"));

        harness.Clock.Advance(TimeSpan.FromMinutes(5));
        var firstEnd = harness.Clock.GetUtcNow().UtcDateTime;
        await store.EndAsync("ended-sid");

        harness.Clock.Advance(TimeSpan.FromMinutes(5));
        await store.EndAsync("ended-sid");

        var row = Assert.Single(await SessionRowsAsync(harness));
        Assert.False(row.IsActive);
        Assert.Equal(firstEnd, row.EndedAtUtc);
    }

    // A session created already inactive still gets a terminal instant, otherwise no cleanup predicate would
    // ever reach it when it also has no expiration.
    [Fact]
    public async Task Create_AnAlreadyInactiveSession_RecordsATerminalTimestamp()
    {
        await using var harness = await SqliteOperationalStorageHarness.CreateConcreteAsync();

        await harness.Storage.GetUserSessionStore(harness.RealmA)
            .CreateAsync(NewSession("inactive-sid", isActive: false));

        var row = Assert.Single(await SessionRowsAsync(harness));
        Assert.False(row.IsActive);
        Assert.NotNull(row.EndedAtUtc);
    }

    // Subject-wide revocation records the terminal instant too, and only for the sessions it actually ended.
    [Fact]
    public async Task EndSessionsForSubject_RecordsTheTerminalTimestampOnlyForTheSessionsItEnds()
    {
        await using var harness = await SqliteOperationalStorageHarness.CreateConcreteAsync();
        var store = harness.Storage.GetUserSessionStore(harness.RealmA);
        await store.CreateAsync(NewSession("sid-kept", "subject-1"));
        await store.CreateAsync(NewSession("sid-revoked", "subject-1"));

        harness.Clock.Advance(TimeSpan.FromMinutes(7));
        var revokedAt = harness.Clock.GetUtcNow().UtcDateTime;
        var ended = await store.EndSessionsForSubjectAsync("subject-1", "sid-kept");

        Assert.Equal(1, ended);
        var rows = await SessionRowsAsync(harness);
        Assert.Null(rows.Single(row => row.SessionId == "sid-kept").EndedAtUtc);
        Assert.Equal(revokedAt, rows.Single(row => row.SessionId == "sid-revoked").EndedAtUtc);
    }

    // The session graph is relational, so recording a client does not rewrite the session row — a touch and a
    // record-client racing cannot lose each other's change.
    [Fact]
    public async Task RecordClient_DoesNotOverwriteTheSessionRow()
    {
        await using var harness = await SqliteOperationalStorageHarness.CreateConcreteAsync();
        var store = harness.Storage.GetUserSessionStore(harness.RealmA);
        await store.CreateAsync(NewSession("interleaved-sid"));

        var touchedAt = Start.AddMinutes(20);
        var expiresAt = Start.AddHours(4);
        await store.TouchAsync("interleaved-sid", touchedAt, expiresAt);
        harness.Clock.Advance(TimeSpan.FromMinutes(30));
        await store.RecordClientAsync("interleaved-sid", "client-a");

        var found = await store.FindByIdAsync("interleaved-sid");
        Assert.NotNull(found);
        Assert.Equal(touchedAt, found.LastSeenAt);
        Assert.Equal(expiresAt, found.ExpiresAt);
        Assert.Single(found.Clients);
    }

    // ...and ending a session does not discard the clients recorded on it.
    [Fact]
    public async Task End_KeepsTheRecordedClients()
    {
        await using var harness = await SqliteOperationalStorageHarness.CreateConcreteAsync();
        var store = harness.Storage.GetUserSessionStore(harness.RealmA);
        await store.CreateAsync(NewSession("ended-with-clients"));
        await store.RecordClientAsync("ended-with-clients", "client-a");

        var ended = await store.EndAsync("ended-with-clients");

        Assert.NotNull(ended);
        Assert.False(ended.IsActive);
        Assert.Single(ended.Clients);
    }

    // SS-03: repeated records converge on one row, keeping the first sighting and refreshing the last.
    [Fact]
    public async Task RecordClient_Repeatedly_KeepsOneRow_PreservingFirstSeenAt()
    {
        await using var harness = await SqliteOperationalStorageHarness.CreateConcreteAsync();
        var store = harness.Storage.GetUserSessionStore(harness.RealmA);
        await store.CreateAsync(NewSession("record-sid"));

        var firstSeen = harness.Clock.GetUtcNow().UtcDateTime;
        await store.RecordClientAsync("record-sid", "client-a");
        harness.Clock.Advance(TimeSpan.FromMinutes(15));
        await store.RecordClientAsync("record-sid", "client-a");
        harness.Clock.Advance(TimeSpan.FromMinutes(15));
        var lastSeen = harness.Clock.GetUtcNow().UtcDateTime;
        await store.RecordClientAsync("record-sid", "client-a");

        var row = Assert.Single(await ClientRowsAsync(harness));
        Assert.Equal(firstSeen, row.FirstSeenAtUtc);
        Assert.Equal(lastSeen, row.LastSeenAtUtc);
    }

    // The last sighting never moves backwards. A writer that captured an earlier instant but reached the
    // database later must not overwrite a newer sighting — here the clock is rewound to stage exactly that,
    // deterministically, without depending on a race to produce the inversion.
    [Fact]
    public async Task RecordClient_WithAnEarlierTimestampArrivingLater_DoesNotMoveLastSeenAtBackwards()
    {
        await using var harness = await SqliteOperationalStorageHarness.CreateConcreteAsync();
        var store = harness.Storage.GetUserSessionStore(harness.RealmA);
        await store.CreateAsync(NewSession("no-regress-sid"));

        harness.Clock.Advance(TimeSpan.FromMinutes(10));
        var later = harness.Clock.GetUtcNow().UtcDateTime;
        await store.RecordClientAsync("no-regress-sid", "client-a");

        // The delayed writer: its instant is older than what is already stored.
        harness.Clock.Now = later.AddMinutes(-5);
        await store.RecordClientAsync("no-regress-sid", "client-a");

        var row = Assert.Single(await ClientRowsAsync(harness));
        Assert.Equal(later, row.LastSeenAtUtc);
    }

    // The same rule on the very first refresh after the insert.
    [Fact]
    public async Task RecordClient_WithAnEarlierTimestampThanTheFirstSighting_KeepsTheGreaterLastSeenAt()
    {
        await using var harness = await SqliteOperationalStorageHarness.CreateConcreteAsync();
        var store = harness.Storage.GetUserSessionStore(harness.RealmA);
        await store.CreateAsync(NewSession("no-regress-first-sid"));

        harness.Clock.Advance(TimeSpan.FromHours(1));
        var firstSeen = harness.Clock.GetUtcNow().UtcDateTime;
        await store.RecordClientAsync("no-regress-first-sid", "client-a");

        harness.Clock.Now = firstSeen.AddMinutes(-30);
        await store.RecordClientAsync("no-regress-first-sid", "client-a");

        var row = Assert.Single(await ClientRowsAsync(harness));
        Assert.Equal(firstSeen, row.FirstSeenAtUtc);
        Assert.Equal(firstSeen, row.LastSeenAtUtc);
    }

    // DF35: the session's clients are owned by it, so purging the session takes them with it.
    [Fact]
    public async Task DeletingASession_CascadesToItsClients()
    {
        await using var harness = await SqliteOperationalStorageHarness.CreateConcreteAsync();
        var store = harness.Storage.GetUserSessionStore(harness.RealmA);
        await store.CreateAsync(NewSession("cascade-sid"));
        await store.RecordClientAsync("cascade-sid", "client-a");
        Assert.Single(await ClientRowsAsync(harness));

        await using (var context = harness.NewOperationalContext())
        {
            await context.Set<UserSessionEntity>()
                .Where(session => session.SessionId == "cascade-sid")
                .ExecuteDeleteAsync();
        }

        Assert.Empty(await ClientRowsAsync(harness));
    }

    // DF5: the same sid in two realms is two independent sessions, and their clients do not mix.
    [Fact]
    public async Task SameSid_InTwoRealms_KeepsClientsSeparate()
    {
        await using var harness = await SqliteOperationalStorageHarness.CreateConcreteAsync();
        await harness.Storage.GetUserSessionStore(harness.RealmA).CreateAsync(NewSession("shared-sid", "subject-a"));
        await harness.Storage.GetUserSessionStore(harness.RealmB).CreateAsync(NewSession("shared-sid", "subject-b"));

        await harness.Storage.GetUserSessionStore(harness.RealmA).RecordClientAsync("shared-sid", "client-a");

        var inA = await harness.Storage.GetUserSessionStore(harness.RealmA).FindByIdAsync("shared-sid");
        var inB = await harness.Storage.GetUserSessionStore(harness.RealmB).FindByIdAsync("shared-sid");

        Assert.NotNull(inA);
        Assert.NotNull(inB);
        Assert.Single(inA.Clients);
        Assert.Empty(inB.Clients);
    }

    // DF5: subject-wide revocation never crosses the realm boundary.
    [Fact]
    public async Task EndSessionsForSubject_NeverCrossesRealms()
    {
        await using var harness = await SqliteOperationalStorageHarness.CreateConcreteAsync();
        await harness.Storage.GetUserSessionStore(harness.RealmA).CreateAsync(NewSession("sid-a", "shared-subject"));
        await harness.Storage.GetUserSessionStore(harness.RealmB).CreateAsync(NewSession("sid-b", "shared-subject"));

        var ended = await harness.Storage.GetUserSessionStore(harness.RealmA)
            .EndSessionsForSubjectAsync("shared-subject", null);

        Assert.Equal(1, ended);
        Assert.True((await harness.Storage.GetUserSessionStore(harness.RealmB).FindByIdAsync("sid-b"))!.IsActive);
    }

    // DF9: what a read returns is an independent graph; mutating it never reaches the database.
    [Fact]
    public async Task MutatingAMaterializedSession_DoesNotPersistImplicitly()
    {
        await using var harness = await SqliteOperationalStorageHarness.CreateConcreteAsync();
        var store = harness.Storage.GetUserSessionStore(harness.RealmA);
        await store.CreateAsync(NewSession("mutate-sid"));

        var first = await store.FindByIdAsync("mutate-sid");
        first!.IsActive = false;
        first.LastSeenAt = Start.AddDays(1);
        first.Clients.Add(new UserSessionClient("injected", Start, Start));

        var second = await store.FindByIdAsync("mutate-sid");
        Assert.True(second!.IsActive);
        Assert.Equal(Start, second.LastSeenAt);
        Assert.Empty(second.Clients);
    }

    // DF19: a pre-cancelled token stops the operation before it reaches the provider.
    [Fact]
    public async Task PreCancelledToken_IsPropagatedToTheProvider()
    {
        await using var harness = await SqliteOperationalStorageHarness.CreateConcreteAsync();
        var store = harness.Storage.GetUserSessionStore(harness.RealmA);
        await store.CreateAsync(NewSession("cancel-sid"));
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await store.FindByIdAsync("cancel-sid", cancelled.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await store.TouchAsync("cancel-sid", Start, null, cancelled.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await store.RecordClientAsync("cancel-sid", "client-a", cancelled.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await store.EndAsync("cancel-sid", cancelled.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await store.EndSessionsForSubjectAsync("subject-a", null, cancelled.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await store.CreateAsync(NewSession("cancel-sid-2"), cancelled.Token));
    }
}
