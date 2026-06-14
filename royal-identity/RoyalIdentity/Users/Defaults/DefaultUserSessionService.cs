using System.Security.Claims;
using RoyalIdentity.Contracts;
using RoyalIdentity.Contracts.Storage;
using RoyalIdentity.Users.Contracts;
using RoyalIdentity.Utils;

namespace RoyalIdentity.Users.Defaults;

/// <summary>
/// Orchestrates the SSO session for the ambient realm (ADR-014 §2.5/2.6). It resolves the realm via
/// <see cref="ICurrentRealmAccessor"/> — never a method parameter — and delegates persistence to the pure
/// <see cref="IUserSessionStore"/>. "Session valid" = a session exists for the principal's <c>sid</c> and
/// is active; an absent session is invalid.
/// </summary>
public sealed class DefaultUserSessionService(
    IStorage storage,
    ICurrentRealmAccessor realmAccessor,
    TimeProvider clock) : IUserSessionService
{
    private IUserSessionStore Store => storage.GetUserSessionStore(realmAccessor.GetCurrentRealm());

    public Task<UserSession?> GetCurrentAsync(ClaimsPrincipal principal, CancellationToken ct = default)
    {
        var sid = GetSessionId(principal);
        return sid is null
            ? Task.FromResult<UserSession?>(null)
            : Store.FindByIdAsync(sid, ct);
    }

    public async Task<bool> IsSessionValidAsync(ClaimsPrincipal principal, CancellationToken ct = default)
    {
        var sid = GetSessionId(principal);
        if (sid is null)
            return false;

        var session = await Store.FindByIdAsync(sid, ct);
        return session is { IsActive: true };
    }

    public Task<UserSession> StartAsync(
        Subject subject, string authenticationMethod, string identityProvider, CancellationToken ct = default)
    {
        var session = new UserSession
        {
            Id = CryptoRandom.CreateUniqueId(16),
            SubjectId = subject.SubjectId,
            AuthenticationMethod = authenticationMethod,
            IdentityProvider = identityProvider,
            StartedAt = clock.GetUtcNow().UtcDateTime,
        };

        return Store.CreateAsync(session, ct);
    }

    public Task<UserSession?> EndAsync(string sessionId, CancellationToken ct = default)
        => Store.EndAsync(sessionId, ct);

    public Task RecordClientAsync(string sessionId, string clientId, CancellationToken ct = default)
        => Store.RecordClientAsync(sessionId, clientId, ct);

    private static string? GetSessionId(ClaimsPrincipal principal)
        => (principal.Identity as ClaimsIdentity)?.FindFirst(JwtRegisteredClaimNames.Sid)?.Value;
}
