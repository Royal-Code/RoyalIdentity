using RoyalIdentity.UserAccounts.Options;

namespace RoyalIdentity.UserAccounts.Infrastructure.Audit;

/// <summary>
/// A single security audit record produced from a committed domain event (ADR-017 §2.11 / Q8). It carries only
/// non-sensitive metadata — <b>never</b> passwords, hashes or action tokens (invariant). The durable store is
/// deferred (<c>plan-data-persistence</c>); the default sink is a no-op until a host registers one.
/// </summary>
/// <param name="RealmId">The realm the event belongs to.</param>
/// <param name="SubjectId">The subject the event concerns, when applicable.</param>
/// <param name="Category">The single audit category this entry falls under.</param>
/// <param name="EventType">A stable event type name (the domain event class name).</param>
/// <param name="OccurredAt">When the domain event occurred.</param>
public sealed record SecurityAuditEntry(
    string RealmId,
    string? SubjectId,
    SecurityAuditCategories Category,
    string EventType,
    DateTimeOffset OccurredAt);
