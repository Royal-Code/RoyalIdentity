namespace RoyalIdentity.Users;

/// <summary>
/// Serializable SSO session model (camada C). Unlike the legacy <c>IdentitySession</c> (removed), it holds
/// the <see cref="SubjectId"/> (a value), not the live user object, so it can be persisted by the future
/// RoyalIdentity.Data.Operational module. Realm scoping is by the store it lives in — never a field/param.
/// </summary>
public sealed class UserSession
{
    /// <summary>Unique session identifier — the OIDC <c>sid</c> claim value.</summary>
    public required string Id { get; init; }

    /// <summary>The subject (OIDC <c>sub</c>) that owns the session. A value, not the user object.</summary>
    public required string SubjectId { get; init; }

    /// <summary>Authentication method reference (OIDC <c>amr</c>) used to start the session.</summary>
    public required string AuthenticationMethod { get; init; }

    /// <summary>Identity provider (OIDC <c>idp</c>) that authenticated the subject.</summary>
    public required string IdentityProvider { get; init; }

    /// <summary>When the session started (drives the <c>auth_time</c> claim).</summary>
    public required DateTime StartedAt { get; init; }

    /// <summary>
    /// When the session was last seen. Drives the realm idle timeout with a write throttle (ADR-017 §2.12): the
    /// store only updates it once per <c>Session.IdleTouchIntervalMinutes</c> window.
    /// </summary>
    public DateTime LastSeenAt { get; set; }

    /// <summary>Security stamp captured when the session starts, when the user provider exposes one.</summary>
    public string? SecurityStamp { get; init; }

    /// <summary>Whether the session is active. Logout/expiry set this to <c>false</c>.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Clients the subject signed into during the session. Deduplicated by client id (the default set
    /// uses <see cref="UserSessionClient"/>'s by-client-id equality).
    /// </summary>
    public HashSet<UserSessionClient> Clients { get; init; } = [];

    /// <summary>
    /// When the SSO session expires (Realm-only — ADR-017 §2.12). Set at sign-in to
    /// <c>StartedAt + Session.SsoSessionMaxMinutes</c> when the realm enables SSO session expiration; the idle
    /// timeout may pull it earlier on touch (never past the max). <c>null</c> means no SSO session lifetime is
    /// enforced (the per-client <c>UserSsoLifetime</c> still forces re-interaction in the <c>PromptLoginDecorator</c>,
    /// orthogonally). The session-validity rule reads this field directly; it does not re-read realm policy for the cap.
    /// </summary>
    public DateTime? ExpiresAt { get; set; }
}
