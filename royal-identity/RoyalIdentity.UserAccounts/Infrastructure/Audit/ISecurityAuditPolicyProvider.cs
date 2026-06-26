using RoyalIdentity.UserAccounts.Options;

namespace RoyalIdentity.UserAccounts.Infrastructure.Audit;

/// <summary>
/// Resolves the enabled audit categories for a realm (Q8: defaults on for security categories, off for the rest).
/// The pure module cannot resolve realm options on its own, so this port is the seam: the default returns all
/// security categories, and the integration replaces it with a realm-aware provider reading
/// <c>UserAccountsRealmOptions.SecurityLifecycle.AuditCategories</c>.
/// </summary>
public interface ISecurityAuditPolicyProvider
{
    /// <summary>
    /// Gets the audit categories enabled for the given realm.
    /// </summary>
    /// <param name="realmId">The realm identifier.</param>
    SecurityAuditCategories GetCategories(string realmId);
}
