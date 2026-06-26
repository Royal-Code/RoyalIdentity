using RoyalIdentity.UserAccounts.Options;

namespace RoyalIdentity.UserAccounts.Infrastructure.Audit;

/// <summary>
/// Default <see cref="ISecurityAuditPolicyProvider"/> used when the module runs without the integration: enables all
/// security categories for every realm (Q8 default). The integration replaces this with a realm-aware provider.
/// </summary>
public sealed class DefaultSecurityAuditPolicyProvider : ISecurityAuditPolicyProvider
{
    /// <inheritdoc />
    public SecurityAuditCategories GetCategories(string realmId) => SecurityAuditCategories.All;
}
