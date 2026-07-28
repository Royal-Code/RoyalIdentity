using Microsoft.EntityFrameworkCore;
using RoyalIdentity.Storage.EntityFramework.Sqlite;

namespace Tests.Integration.Prepare;

/// <summary>
/// Read-only Operational probe for assertions that public protocol/store APIs cannot express, such as finding
/// the session created for a subject before its sid is known. It never prepares or mutates relational state.
/// </summary>
internal sealed class PersistentOperationalProbe(OperationalSqliteDbContext db)
{
    public async Task<IReadOnlyList<PersistentSessionState>> FindSessionsAsync(
        string realmId,
        string subjectId,
        CancellationToken ct)
    {
        var sessions = await db.UserSessions
            .AsNoTracking()
            .Where(session => session.RealmId == realmId && session.SubjectId == subjectId)
            .OrderBy(session => session.StartedAtUtc)
            .ToListAsync(ct);
        var sessionIds = sessions.Select(session => session.SessionId).ToArray();
        var clients = await db.UserSessionClients
            .AsNoTracking()
            .Where(client => client.RealmId == realmId && sessionIds.Contains(client.SessionId))
            .ToListAsync(ct);

        return sessions
            .Select(session => new PersistentSessionState(
                session.SessionId,
                session.SubjectId,
                session.AuthenticationMethod,
                session.IsActive,
                clients
                    .Where(client => client.SessionId == session.SessionId)
                    .Select(client => client.ClientId)
                    .ToHashSet(StringComparer.Ordinal)))
            .ToArray();
    }
}

public sealed record PersistentSessionState(
    string Id,
    string SubjectId,
    string AuthenticationMethod,
    bool IsActive,
    IReadOnlySet<string> ClientIds);
