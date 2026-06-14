using System.Security.Claims;

namespace RoyalIdentity.Users.Contracts;

/// <summary>
/// Orchestrates the SSO session for the ambient realm — the realm is resolved via
/// <c>ICurrentRealmAccessor</c>, never a method parameter (ADR-014 §2.5). Sits on top of the pure
/// session store. "Session valid" means a session exists for the principal's <c>sid</c> and is active;
/// an absent session is invalid.
/// </summary>
public interface IUserSessionService
{
    /// <summary>Gets the current session for the principal, or <c>null</c> when none exists.</summary>
    Task<UserSession?> GetCurrentAsync(ClaimsPrincipal principal, CancellationToken ct = default);

    /// <summary>Whether the principal's session exists and is active (absent ⇒ invalid).</summary>
    Task<bool> IsSessionValidAsync(ClaimsPrincipal principal, CancellationToken ct = default);

    /// <summary>Starts a new session for the subject with the given auth method and identity provider.</summary>
    Task<UserSession> StartAsync(
        Subject subject, string authenticationMethod, string identityProvider, CancellationToken ct = default);

    /// <summary>Ends the session with the given id and returns it, or <c>null</c> when not found.</summary>
    Task<UserSession?> EndAsync(string sessionId, CancellationToken ct = default);

    /// <summary>Records that the subject signed into the given client during the session (dedup by client).</summary>
    Task RecordClientAsync(string sessionId, string clientId, CancellationToken ct = default);
}
