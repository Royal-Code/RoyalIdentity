namespace RoyalIdentity.Users.Contracts;

/// <summary>
/// Pure session store (camada C): persists <see cref="UserSession"/> by <c>sid</c>, without touching
/// <c>HttpContext</c> and without a notion of "current" (ADR-014 §2.6). Realm is bound at construction
/// (via <c>IStorage.GetUserSessionStore(realm)</c>); the methods take no realm parameter. The notion of
/// the current session and "session valid" lives in <see cref="IUserSessionService"/>.
/// </summary>
public interface IUserSessionStore
{
    /// <summary>Persists a new session and returns it.</summary>
    Task<UserSession> CreateAsync(UserSession session, CancellationToken ct = default);

    /// <summary>Gets the session by its <c>sid</c>, or <c>null</c> when not found.</summary>
    Task<UserSession?> FindByIdAsync(string sessionId, CancellationToken ct = default);

    /// <summary>Records that the subject signed into the given client during the session (deduplicated by client).</summary>
    Task RecordClientAsync(string sessionId, string clientId, CancellationToken ct = default);

    /// <summary>Ends the session (marks it inactive) and returns it, or <c>null</c> when not found.</summary>
    Task<UserSession?> EndAsync(string sessionId, CancellationToken ct = default);

    /// <summary>
    /// Persists an idle touch: updates <c>LastSeenAt</c> and the recomputed <c>ExpiresAt</c> for the session. The
    /// caller applies the throttle (it only calls this once per idle-touch window — ADR-017 §2.12). A no-op when the
    /// session does not exist.
    /// </summary>
    /// <param name="sessionId">The session <c>sid</c>.</param>
    /// <param name="lastSeenAt">The new last-seen timestamp.</param>
    /// <param name="expiresAt">The recomputed expiration, or <c>null</c> when no SSO lifetime applies.</param>
    /// <param name="ct">The cancellation token.</param>
    Task TouchAsync(string sessionId, DateTime lastSeenAt, DateTime? expiresAt, CancellationToken ct = default);

    /// <summary>
    /// Ends all active sessions of the subject, optionally preserving one <c>sid</c> (Q13 active revocation).
    /// Idempotent: already-inactive sessions are skipped. Returns the number of sessions ended.
    /// </summary>
    /// <param name="subjectId">The subject whose sessions are ended.</param>
    /// <param name="exceptSessionId">A <c>sid</c> to preserve (the current session), or <c>null</c> to end all.</param>
    /// <param name="ct">The cancellation token.</param>
    Task<int> EndSessionsForSubjectAsync(string subjectId, string? exceptSessionId, CancellationToken ct = default);
}
