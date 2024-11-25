using Microsoft.AspNetCore.Http;
using RoyalIdentity.Extensions;
using RoyalIdentity.Users;
using RoyalIdentity.Users.Contracts;
using RoyalIdentity.Utils;

namespace RoyalIdentity.Storage.InMemory;

public class UserSessionStore : IUserSessionStore
{
    private readonly MemoryStorage memoryStorage;
    private readonly TimeProvider clock;
    private readonly IHttpContextAccessor accessor;

    public UserSessionStore(MemoryStorage memoryStorage, TimeProvider clock, IHttpContextAccessor accessor)
    {
        this.memoryStorage = memoryStorage;
        this.clock = clock;
        this.accessor = accessor;
    }

    public Task AddClientIdAsync(string sessionId, string clientId, CancellationToken ct = default)
    {
        memoryStorage.UserSessions.TryGetValue(sessionId, out var session);
        if (session is not null)
        {
            session.Clients.Add(clientId);
        }
        return Task.CompletedTask;
    }

    public Task<IdentitySession> StartSessionAsync(string username, CancellationToken ct = default)
    {
        var sid = Base64Url.Encode(CryptoRandom.CreateRandomKey(16));

        var session = new IdentitySession
        {
            Id = sid,
            Username = username,
            StartedAt = clock.GetUtcNow().UtcDateTime,
        };

        memoryStorage.UserSessions[sid] = session;

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
        memoryStorage.UserSessions.TryGetValue(sessionId, out var session);
        return new ValueTask<IdentitySession?>(session);
    }
}