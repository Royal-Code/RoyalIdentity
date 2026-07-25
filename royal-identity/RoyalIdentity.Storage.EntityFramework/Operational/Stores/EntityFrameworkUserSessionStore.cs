using Microsoft.EntityFrameworkCore;
using RoyalIdentity.Data.Operational.Entities;
using RoyalIdentity.Models;
using RoyalIdentity.Users;
using RoyalIdentity.Users.Contracts;

namespace RoyalIdentity.Storage.EntityFramework.Operational.Stores;

/// <summary>
/// Realm-bound SSO session store over <c>user_sessions</c> and its <c>user_session_clients</c> child table
/// (matrix SS-01..SS-06, ADR-014/017).
/// <para>
/// Nothing about a session is opaque, so it carries no protected payload: every field is a queryable column and
/// the clients it signed into are rows of their own. That is what lets record-client, touch and end be
/// set-based conditional operations instead of read-modify-write over one serialized blob — so two of them
/// racing cannot lose each other's change (plan DF15/DF36).
/// </para>
/// </summary>
internal sealed class EntityFrameworkUserSessionStore(
    Realm realm,
    IOperationalDbContextAccessor accessor,
    TimeProvider clock) : IUserSessionStore
{
    public async Task<UserSession> CreateAsync(UserSession session, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(session);

        var db = accessor.DbContext;
        var row = new UserSessionEntity
        {
            RealmId = realm.Id,
            SessionId = session.Id,
            SubjectId = session.SubjectId,
            AuthenticationMethod = session.AuthenticationMethod,
            IdentityProvider = session.IdentityProvider,
            StartedAtUtc = session.StartedAt,
            LastSeenAtUtc = session.LastSeenAt,
            ExpiresAtUtc = session.ExpiresAt,
            // A session created already inactive has reached its terminal state at creation. Recording that
            // instant here is what keeps it eligible for cleanup (plan DF17); leaving it null would make an
            // inactive session without expiration unreachable by every eligibility predicate.
            EndedAtUtc = session.IsActive ? null : clock.GetUtcNow().UtcDateTime,
            SecurityStamp = session.SecurityStamp,
            IsActive = session.IsActive,
        };

        var clientRows = session.Clients
            .Select(client => new UserSessionClientEntity
            {
                RealmId = realm.Id,
                SessionId = session.Id,
                ClientId = client.ClientId,
                FirstSeenAtUtc = client.FirstSeenAt,
                LastSeenAtUtc = client.LastSeenAt,
            })
            .ToList();

        db.Add(row);
        db.AddRange(clientRows);
        try
        {
            // SS-01 is create-only: the primary key is the authority, so a duplicate sid in the same realm
            // fails visibly instead of replacing a live session.
            await db.SaveChangesAsync(ct);
        }
        finally
        {
            db.Entry(row).State = EntityState.Detached;
            foreach (var clientRow in clientRows)
                db.Entry(clientRow).State = EntityState.Detached;
        }

        return session;
    }

    public async Task<UserSession?> FindByIdAsync(string sessionId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(sessionId);

        // SS-02: neither expiration nor the active flag filters the read — an expired or ended session stays
        // readable until cleanup, and the validity rule belongs to the session service (ADR-017).
        var row = await Sessions()
            .SingleOrDefaultAsync(session => session.SessionId == sessionId, ct);

        if (row is null)
            return null;

        var clients = await SessionClients()
            .Where(client => client.SessionId == sessionId)
            .ToListAsync(ct);

        return Materialize(row, clients);
    }

    public async Task RecordClientAsync(string sessionId, string clientId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(sessionId);
        ArgumentException.ThrowIfNullOrEmpty(clientId);

        // SS-03: recording on an absent session is a no-op, never an error.
        if (!await Sessions().AnyAsync(session => session.SessionId == sessionId, ct))
            return;

        var seenAt = clock.GetUtcNow().UtcDateTime;

        // Deduplication is the composite primary key itself, and refreshing an existing entry never touches
        // FirstSeenAt — so the first sighting survives however many times the client is recorded.
        if (await TouchClientAsync(sessionId, clientId, seenAt, ct) is not 0)
            return;

        var db = accessor.DbContext;
        var row = new UserSessionClientEntity
        {
            RealmId = realm.Id,
            SessionId = sessionId,
            ClientId = clientId,
            FirstSeenAtUtc = seenAt,
            LastSeenAtUtc = seenAt,
        };

        db.Add(row);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Two races end up here. A concurrent writer may have inserted the same client first — the key
            // constraint prevented the duplicate, and this caller just refreshes the winner's entry. Or the
            // session may have been removed (cleanup, realm purge) after the pre-check, and the foreign key
            // rejected the child; that is not a torn state — the constraint did its job — and it linearizes as
            // the absent session SS-03 already defines as a no-op. Anything else must surface.
            if (await TouchClientAsync(sessionId, clientId, seenAt, ct) is not 0)
                return;

            if (await Sessions().AnyAsync(session => session.SessionId == sessionId, ct))
                throw;
        }
        finally
        {
            db.Entry(row).State = EntityState.Detached;
        }
    }

    public async Task TouchAsync(
        string sessionId, DateTime lastSeenAt, DateTime? expiresAt, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(sessionId);

        // SS-05: the caller owns both timestamps (it also owns the throttle window — ADR-017 §2.12), so the
        // store never substitutes its own clock here. An absent session is a no-op.
        await Sessions()
            .Where(session => session.SessionId == sessionId)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(session => session.LastSeenAtUtc, lastSeenAt)
                    .SetProperty(session => session.ExpiresAtUtc, expiresAt),
                ct);
    }

    public async Task<UserSession?> EndAsync(string sessionId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(sessionId);

        // Conditional on still being active, so repeating the call changes nothing and the first terminal
        // timestamp is preserved (plan DF15/DF17).
        await Sessions()
            .Where(session => session.SessionId == sessionId && session.IsActive)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(session => session.IsActive, false)
                    .SetProperty(session => session.EndedAtUtc, clock.GetUtcNow().UtcDateTime),
                ct);

        // SS-04: returns the session either way — already inactive on a repeat, null when absent.
        return await FindByIdAsync(sessionId, ct);
    }

    public async Task<int> EndSessionsForSubjectAsync(
        string subjectId, string? exceptSessionId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(subjectId);

        var query = Sessions()
            .Where(session => session.SubjectId == subjectId && session.IsActive);

        if (exceptSessionId is not null)
            query = query.Where(session => session.SessionId != exceptSessionId);

        // SS-06: the count is of effective transitions only — sessions already inactive are not counted, so a
        // repeat returns zero.
        return await query.ExecuteUpdateAsync(
            setters => setters
                .SetProperty(session => session.IsActive, false)
                .SetProperty(session => session.EndedAtUtc, clock.GetUtcNow().UtcDateTime),
            ct);
    }

    private static UserSession Materialize(UserSessionEntity row, List<UserSessionClientEntity> clients)
        => new()
        {
            Id = row.SessionId,
            SubjectId = row.SubjectId,
            AuthenticationMethod = row.AuthenticationMethod,
            IdentityProvider = row.IdentityProvider,
            StartedAt = row.StartedAtUtc,
            LastSeenAt = row.LastSeenAtUtc,
            ExpiresAt = row.ExpiresAtUtc,
            SecurityStamp = row.SecurityStamp,
            IsActive = row.IsActive,
            Clients = [.. clients.Select(client =>
                new UserSessionClient(client.ClientId, client.FirstSeenAtUtc, client.LastSeenAtUtc))],
        };

    /// <summary>
    /// Refreshes an existing entry's last sighting, never moving it backwards: the update takes the greater of
    /// the stored value and this writer's instant. Assigning unconditionally would let a writer that captured
    /// an earlier timestamp but committed later overwrite a newer sighting, which is exactly the regression the
    /// "greatest/latest LastSeenAt" acceptance forbids. <c>FirstSeenAt</c> is untouched by design — the first
    /// entry persisted is the first sighting.
    /// </summary>
    private Task<int> TouchClientAsync(string sessionId, string clientId, DateTime seenAt, CancellationToken ct)
        => SessionClients()
            .Where(client => client.SessionId == sessionId && client.ClientId == clientId)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(
                    client => client.LastSeenAtUtc,
                    client => client.LastSeenAtUtc > seenAt ? client.LastSeenAtUtc : seenAt),
                ct);

    private IQueryable<UserSessionEntity> Sessions()
        => accessor.DbContext.Set<UserSessionEntity>()
            .AsNoTracking()
            .Where(session => session.RealmId == realm.Id);

    private IQueryable<UserSessionClientEntity> SessionClients()
        => accessor.DbContext.Set<UserSessionClientEntity>()
            .AsNoTracking()
            .Where(client => client.RealmId == realm.Id);
}
