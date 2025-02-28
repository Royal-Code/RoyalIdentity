using Microsoft.AspNetCore.Http;
using RoyalIdentity.Extensions;
using RoyalIdentity.Users;
using RoyalIdentity.Users.Contracts;
using RoyalIdentity.Utils;
using System.Collections.Concurrent;

namespace RoyalIdentity.Storage.InMemory;

public class UserSessionStore : IUserSessionStore
{
    private readonly ConcurrentDictionary<string, IdentitySession> userSessions;
    private readonly TimeProvider clock;
    private readonly IHttpContextAccessor accessor;

    public UserSessionStore(
        ConcurrentDictionary<string, IdentitySession> userSessions,
        TimeProvider clock, 
        IHttpContextAccessor accessor)
    {
        this.userSessions = userSessions;
        this.clock = clock;
        this.accessor = accessor;
    }

    public Task AddClientIdAsync(string sessionId, string clientId, CancellationToken ct = default)
    {
        userSessions.TryGetValue(sessionId, out var session);
        session?.Clients.Add(clientId);
        return Task.CompletedTask;
    }

    public Task<IdentitySession> StartSessionAsync(IdentityUser user, string amr, CancellationToken ct = default)
    {
        var sid = CryptoRandom.CreateUniqueId(16);

        var session = new IdentitySession
        {
            Id = sid,
            User = user,
            Amr = amr,
            StartedAt = clock.GetUtcNow().UtcDateTime,
        };

        userSessions[sid] = session;

        return Task.FromResult(session);
    }

    public async Task<IdentitySession?> EndSessionAsync(string sessionId, CancellationToken ct = default)
    {
        var session = await GetUserSessionAsync(sessionId, ct);
        if (session is not null)
            session.IsActive = false;

        return session;
    }

    public ValueTask<IdentitySession?> GetCurrentSessionAsync(CancellationToken ct)
    {
        IdentitySession? session = null;
        var httpContext = accessor.HttpContext;

        return httpContext is not null && httpContext.User.IsAuthenticated()
            ? GetUserSessionAsync(httpContext.User.GetSessionId(), ct)
            : new ValueTask<IdentitySession?>(session);
    }

    public ValueTask<IdentitySession?> GetUserSessionAsync(string sessionId, CancellationToken ct)
    {
        userSessions.TryGetValue(sessionId, out var session);
        return new ValueTask<IdentitySession?>(session);
    }
}