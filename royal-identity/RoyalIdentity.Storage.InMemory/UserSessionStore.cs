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
        var sid = Convert.ToBase64String(CryptoRandom.CreateRandomKey(16));

        var session = new IdentitySession
        {
            Id = sid,
            Username = username,
            StartedAt = clock.GetUtcNow().UtcDateTime,
        };

        memoryStorage.UserSessions[sid] = session;

        return Task.FromResult(session);
    }

    public ValueTask<IdentitySession?> GetCurrentSessionAsync(CancellationToken ct)
    {
        IdentitySession? session = null;
        var httpContext = accessor.HttpContext;
        if (httpContext is null)
            return new ValueTask<IdentitySession?>(session);

        if (!httpContext.User.IsAuthenticated())
            return new ValueTask<IdentitySession?>(session);

        memoryStorage.UserSessions.TryGetValue(httpContext.User.GetSessionId(), out session);
        return new ValueTask<IdentitySession?>(session);
    }
}