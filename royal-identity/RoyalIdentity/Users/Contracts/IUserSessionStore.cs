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
}
