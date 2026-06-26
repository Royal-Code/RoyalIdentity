namespace RoyalIdentity.UserAccounts.Infrastructure.Audit;

/// <summary>
/// Default <see cref="ISecurityAuditSink"/> that discards entries. Keeps the module self-contained (no logging
/// dependency) until a host registers a real sink; the durable store is deferred to <c>plan-data-persistence</c>.
/// </summary>
public sealed class NoopSecurityAuditSink : ISecurityAuditSink
{
    /// <inheritdoc />
    public Task WriteAsync(SecurityAuditEntry entry, CancellationToken ct = default) => Task.CompletedTask;
}
