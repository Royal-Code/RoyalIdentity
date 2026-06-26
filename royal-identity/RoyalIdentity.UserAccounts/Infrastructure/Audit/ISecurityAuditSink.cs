namespace RoyalIdentity.UserAccounts.Infrastructure.Audit;

/// <summary>
/// Destination for security audit entries (ADR-017 §2.11 / Q8). The module produces entries via
/// <see cref="SecurityAuditObserver"/>; the host provides the sink (log, store, telemetry). The default
/// (<see cref="NoopSecurityAuditSink"/>) discards entries until a real sink is registered. The durable, queryable
/// store is deferred to <c>plan-data-persistence</c>.
/// </summary>
public interface ISecurityAuditSink
{
    /// <summary>
    /// Writes one audit entry. Implementations should be resilient — auditing must not break the request.
    /// </summary>
    /// <param name="entry">The audit entry (never carries secrets).</param>
    /// <param name="ct">The cancellation token.</param>
    Task WriteAsync(SecurityAuditEntry entry, CancellationToken ct = default);
}
