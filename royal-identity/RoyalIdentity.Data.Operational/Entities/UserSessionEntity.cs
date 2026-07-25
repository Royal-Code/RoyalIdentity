namespace RoyalIdentity.Data.Operational.Entities;

/// <summary>
/// SSO session row (table <c>user_sessions</c>, plan DF15/DF36). Every field of the core session model maps
/// to a queryable column and the clients it signed into live in <see cref="UserSessionClientEntity"/>, so the
/// session carries no protected payload: nothing about it is opaque.
/// </summary>
public class UserSessionEntity
{
    /// <summary>The realm this session belongs to. A logical link by value (plan DF6).</summary>
    public required string RealmId { get; set; }

    /// <summary>The OIDC <c>sid</c>.</summary>
    public required string SessionId { get; set; }

    public required string SubjectId { get; set; }

    public required string AuthenticationMethod { get; set; }

    public required string IdentityProvider { get; set; }

    public DateTime StartedAtUtc { get; set; }

    public DateTime LastSeenAtUtc { get; set; }

    /// <summary>Absolute SSO expiration, or <c>null</c> when no SSO session lifetime is enforced (ADR-017 §2.12).</summary>
    public DateTime? ExpiresAtUtc { get; set; }

    /// <summary>
    /// When the session reached its terminal state through logout or revocation. Repeated end/revocation keeps
    /// the first timestamp; cleanup uses it as an indexable eligibility predicate (plan DF17).
    /// </summary>
    public DateTime? EndedAtUtc { get; set; }

    public string? SecurityStamp { get; set; }

    public bool IsActive { get; set; }
}
