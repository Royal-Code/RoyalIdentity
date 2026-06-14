using System.Collections.Concurrent;
using RoyalIdentity.Users;
using RoyalIdentity.Users.Contracts;

namespace RoyalIdentity.Storage.InMemory;

/// <summary>
/// In-memory (fake/reference) pure <see cref="IUserSessionStore"/>: persists <see cref="UserSession"/> by
/// <c>sid</c>. No <c>HttpContext</c>, no "current session" (ADR-014 §2.6). Realm is bound at construction
/// (the sessions dictionary belongs to one realm).
/// </summary>
public class UserSessionStore(
    ConcurrentDictionary<string, UserSession> userSessions,
    TimeProvider clock) : IUserSessionStore
{
    public Task<UserSession> CreateAsync(UserSession session, CancellationToken ct = default)
    {
        userSessions[session.Id] = session;
        return Task.FromResult(session);
    }

    public Task<UserSession?> FindByIdAsync(string sessionId, CancellationToken ct = default)
    {
        userSessions.TryGetValue(sessionId, out var session);
        return Task.FromResult(session);
    }

    public Task RecordClientAsync(string sessionId, string clientId, CancellationToken ct = default)
    {
        if (userSessions.TryGetValue(sessionId, out var session))
        {
            var now = clock.GetUtcNow().UtcDateTime;
            var existing = session.Clients.FirstOrDefault(c => c.ClientId == clientId);

            // Deduplicate by client id (UserSessionClient equality is by ClientId); refresh LastSeenAt.
            if (existing is not null)
                session.Clients.Remove(existing);

            session.Clients.Add(new UserSessionClient(clientId, existing?.FirstSeenAt ?? now, now));
        }

        return Task.CompletedTask;
    }

    public Task<UserSession?> EndAsync(string sessionId, CancellationToken ct = default)
    {
        userSessions.TryGetValue(sessionId, out var session);
        if (session is not null)
            session.IsActive = false;

        return Task.FromResult(session);
    }
}
