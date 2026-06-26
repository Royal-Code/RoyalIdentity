using System.Security.Claims;
using RoyalIdentity.Contracts;
using RoyalIdentity.Contracts.Storage;
using RoyalIdentity.Options;
using RoyalIdentity.Users.Contracts;
using RoyalIdentity.Security.Cryptography;
using RoyalIdentity.Utils;

namespace RoyalIdentity.Users.Defaults;

/// <summary>
/// Orchestrates the SSO session for the ambient realm (ADR-014 §2.5/2.6). It resolves the realm via
/// <see cref="ICurrentRealmAccessor"/> — never a method parameter — and delegates persistence to the pure
/// <see cref="IUserSessionStore"/>. "Session valid" = a session exists for the principal's <c>sid</c>, is active, is
/// not past its SSO lifetime (Realm-only — ADR-017 §2.12), and — when the realm enforces it — was started at/after the
/// account's <c>SessionsValidAfter</c> marker, read through the optional <see cref="IUserSecurityStateProvider"/>
/// capability (Q15). Idle activity is recorded with a write throttle.
/// </summary>
public sealed class DefaultUserSessionService(
    IStorage storage,
    ICurrentRealmAccessor realmAccessor,
    IUserDirectory userDirectory,
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

        var realm = realmAccessor.GetCurrentRealm();
        var store = storage.GetUserSessionStore(realm);

        var session = await store.FindByIdAsync(sid, ct);
        if (session is not { IsActive: true })
            return false;

        var now = clock.GetUtcNow().UtcDateTime;
        var sessionOptions = realm.Options.Session;

        // SSO session expiration (Realm-only). ExpiresAt was set at sign-in and may have been pulled earlier by the
        // idle timeout on previous touches; the rule reads the field directly (no re-read of realm policy for the cap).
        if (sessionOptions.EnableSsoSessionExpiration && session.ExpiresAt is { } expiresAt && now >= expiresAt)
            return false;

        // Passive invalidation by the account security marker, when the realm enforces it (Q7/Q15). The realm
        // requires the capability here (composition is a configuration error otherwise — Q15.3): failing fast keeps a
        // security policy from being silently inoperative. The integration gates SessionsValidAfter by the realm
        // policy, so a non-null value means "enforce".
        if (sessionOptions.EnableSessionInvalidationByState)
        {
            var stateProvider = userDirectory.GetSecurityStateProvider(realm)
                ?? throw new InvalidOperationException(
                    $"Realm '{realm.Id}' enables Session.EnableSessionInvalidationByState but its user provider does " +
                    "not expose IUserSecurityStateProvider (Q15 composition error).");

            var state = await stateProvider.GetSecurityStateAsync(session.SubjectId, ct);
            if (state?.SessionsValidAfter is { } validAfter && session.StartedAt < validAfter.UtcDateTime)
                return false;
        }

        // Idle touch with throttle: only writes once per idle-touch window, and never extends past the SSO max.
        await TouchIdleAsync(store, session, sessionOptions, now, ct);

        return true;
    }

    public Task<UserSession> StartAsync(
        Subject subject, string authenticationMethod, string identityProvider, CancellationToken ct = default)
        => StartAsync(subject, authenticationMethod, identityProvider, securityStamp: null, ct);

    public Task<UserSession> StartAsync(
        Subject subject,
        string authenticationMethod,
        string identityProvider,
        string? securityStamp,
        CancellationToken ct = default)
    {
        var realm = realmAccessor.GetCurrentRealm();
        var startedAt = clock.GetUtcNow().UtcDateTime;

        var session = new UserSession
        {
            Id = CryptoRandom.CreateUniqueId(16),
            SubjectId = subject.SubjectId,
            AuthenticationMethod = authenticationMethod,
            IdentityProvider = identityProvider,
            StartedAt = startedAt,
            LastSeenAt = startedAt,
            SecurityStamp = securityStamp,
            ExpiresAt = ComputeExpiry(realm.Options.Session, startedAt, startedAt),
        };

        return storage.GetUserSessionStore(realm).CreateAsync(session, ct);
    }

    public Task<UserSession?> EndAsync(string sessionId, CancellationToken ct = default)
        => Store.EndAsync(sessionId, ct);

    public Task RecordClientAsync(string sessionId, string clientId, CancellationToken ct = default)
        => Store.RecordClientAsync(sessionId, clientId, ct);

    private async Task TouchIdleAsync(
        IUserSessionStore store, UserSession session, SessionOptions options, DateTime now, CancellationToken ct)
    {
        if (!options.EnableSsoSessionExpiration || options.SsoSessionIdleMinutes <= 0)
            return;

        // Throttle: skip the write until the minimum window has passed since the last touch.
        if (now - session.LastSeenAt < TimeSpan.FromMinutes(options.IdleTouchIntervalMinutes))
            return;

        var newExpiry = ComputeExpiry(options, session.StartedAt, now);
        await store.TouchAsync(session.Id, now, newExpiry, ct);
    }

    /// <summary>
    /// Computes the effective SSO expiry: <c>min(StartedAt + SsoSessionMax, reference + SsoSessionIdle)</c> when idle
    /// is enabled, the max otherwise. Returns <c>null</c> when SSO session expiration is off.
    /// </summary>
    private static DateTime? ComputeExpiry(SessionOptions options, DateTime startedAt, DateTime reference)
    {
        if (!options.EnableSsoSessionExpiration)
            return null;

        var maxExpiry = startedAt.AddMinutes(options.SsoSessionMaxMinutes);
        if (options.SsoSessionIdleMinutes <= 0)
            return maxExpiry;

        var idleExpiry = reference.AddMinutes(options.SsoSessionIdleMinutes);
        return idleExpiry < maxExpiry ? idleExpiry : maxExpiry;
    }

    private static string? GetSessionId(ClaimsPrincipal principal)
        => (principal.Identity as ClaimsIdentity)?.FindFirst(JwtRegisteredClaimNames.Sid)?.Value;
}
