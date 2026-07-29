using System.Collections.Concurrent;
using RoyalIdentity.Contracts.Storage;
using RoyalIdentity.Models.Tokens;
using RoyalIdentity.Options;
using RoyalIdentity.Users;
using RoyalIdentity.Users.Contracts;
using RealmModel = RoyalIdentity.Models.Realm;

namespace Tests.Integration.Users;

/// <summary>
/// Focused contract double for session-service unit tests. It implements only the two ports exercised by
/// session lifecycle and revocation; it is not a general storage backing.
/// </summary>
internal sealed class SessionStorageDouble(TimeProvider clock) : IStorage
{
    private readonly SessionStoreDouble sessionStore = new(clock);
    private readonly RefreshTokenStoreDouble refreshTokenStore = new();

    public ConcurrentDictionary<string, UserSession> Sessions => sessionStore.Sessions;

    public ConcurrentDictionary<string, RefreshToken> RefreshTokens => refreshTokenStore.RefreshTokens;

    public IUserSessionStore GetUserSessionStore(RealmModel realm) => sessionStore;

    public IRefreshTokenStore GetRefreshTokenStore(RealmModel realm) => refreshTokenStore;

    public ServerOptions ServerOptions => throw new NotSupportedException();

    public IRealmStore Realms => throw new NotSupportedException();

    public IAuthorizeParametersStore GetAuthorizeParametersStore(RealmModel realm) => throw new NotSupportedException();

    public IAccessTokenStore GetAccessTokenStore(RealmModel realm) => throw new NotSupportedException();

    public IAuthorizationCodeStore GetAuthorizationCodeStore(RealmModel realm) => throw new NotSupportedException();

    public IUserConsentStore GetUserConsentStore(RealmModel realm) => throw new NotSupportedException();

    public IKeyStore GetKeyStore(RealmModel realm) => throw new NotSupportedException();

    public IClientStore GetClientStore(RealmModel realm) => throw new NotSupportedException();

    public IResourceStore GetResourceStore(RealmModel realm) => throw new NotSupportedException();

    private sealed class SessionStoreDouble(TimeProvider clock) : IUserSessionStore
    {
        public ConcurrentDictionary<string, UserSession> Sessions { get; } = new();

        public Task<UserSession> CreateAsync(UserSession session, CancellationToken ct = default)
        {
            Sessions[session.Id] = session;
            return Task.FromResult(session);
        }

        public Task<UserSession?> FindByIdAsync(string sessionId, CancellationToken ct = default)
        {
            Sessions.TryGetValue(sessionId, out var session);
            return Task.FromResult(session);
        }

        public Task RecordClientAsync(string sessionId, string clientId, CancellationToken ct = default)
        {
            if (!Sessions.TryGetValue(sessionId, out var session))
                return Task.CompletedTask;

            var now = clock.GetUtcNow().UtcDateTime;
            var existing = session.Clients.FirstOrDefault(client => client.ClientId == clientId);
            if (existing is not null)
                session.Clients.Remove(existing);
            session.Clients.Add(new UserSessionClient(clientId, existing?.FirstSeenAt ?? now, now));
            return Task.CompletedTask;
        }

        public Task<UserSession?> EndAsync(string sessionId, CancellationToken ct = default)
        {
            Sessions.TryGetValue(sessionId, out var session);
            if (session is not null)
                session.IsActive = false;
            return Task.FromResult(session);
        }

        public Task TouchAsync(
            string sessionId,
            DateTime lastSeenAt,
            DateTime? expiresAt,
            CancellationToken ct = default)
        {
            if (Sessions.TryGetValue(sessionId, out var session))
            {
                session.LastSeenAt = lastSeenAt;
                session.ExpiresAt = expiresAt;
            }
            return Task.CompletedTask;
        }

        public Task<int> EndSessionsForSubjectAsync(
            string subjectId,
            string? exceptSessionId,
            CancellationToken ct = default)
        {
            var count = 0;
            foreach (var session in Sessions.Values)
            {
                if (session.SubjectId != subjectId ||
                    !session.IsActive ||
                    session.Id == exceptSessionId)
                {
                    continue;
                }

                session.IsActive = false;
                count++;
            }
            return Task.FromResult(count);
        }
    }

    private sealed class RefreshTokenStoreDouble : IRefreshTokenStore
    {
        public ConcurrentDictionary<string, RefreshToken> RefreshTokens { get; } = new();

        public Task StoreAsync(RefreshToken token, CancellationToken ct)
        {
            RefreshTokens.TryAdd(token.Token, token);
            return Task.CompletedTask;
        }

        public Task<RefreshToken?> GetAsync(string token, CancellationToken ct)
        {
            RefreshTokens.TryGetValue(token, out var refreshToken);
            return Task.FromResult(refreshToken);
        }

        public Task<RefreshTokenTransition> TryConsumeAsync(
            string token,
            int expectedStateVersion,
            DateTime consumedAt,
            CancellationToken ct)
        {
            lock (RefreshTokens)
            {
                if (!RefreshTokens.TryGetValue(token, out var current))
                    return Task.FromResult(RefreshTokenTransition.NotFound());

                if (current.ConsumedTime is not null)
                    return Task.FromResult(RefreshTokenTransition.AlreadyConsumed(current));

                if (current.StateVersion != expectedStateVersion)
                    return Task.FromResult(RefreshTokenTransition.Conflict(current));

                current.ConsumedTime = consumedAt;
                current.StateVersion++;
                return Task.FromResult(RefreshTokenTransition.Succeeded(current));
            }
        }

        public Task<RefreshTokenTransition> TryUpdateAsync(
            RefreshToken token,
            int expectedStateVersion,
            CancellationToken ct)
        {
            lock (RefreshTokens)
            {
                if (!RefreshTokens.TryGetValue(token.Token, out var current))
                    return Task.FromResult(RefreshTokenTransition.NotFound());

                if (current.StateVersion != expectedStateVersion)
                    return Task.FromResult(RefreshTokenTransition.Conflict(current));

                token.StateVersion++;
                RefreshTokens[token.Token] = token;
                return Task.FromResult(RefreshTokenTransition.Succeeded(token));
            }
        }

        public Task RemoveAsync(string token, CancellationToken ct)
        {
            RefreshTokens.TryRemove(token, out _);
            return Task.CompletedTask;
        }

        public Task<int> RemoveBySubjectAsync(string subjectId, CancellationToken ct)
        {
            var count = 0;
            foreach (var entry in RefreshTokens)
            {
                if (entry.Value.SubjectId == subjectId &&
                    RefreshTokens.TryRemove(entry.Key, out _))
                {
                    count++;
                }
            }
            return Task.FromResult(count);
        }
    }
}
