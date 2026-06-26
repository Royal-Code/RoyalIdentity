using RoyalIdentity.UserAccounts.Infrastructure.Audit;
using RoyalIdentity.UserAccounts.Options;

namespace RoyalIdentity.UserAccounts.Integration;

/// <summary>
/// Realm-aware <see cref="ISecurityAuditPolicyProvider"/> (Q8): reads the enabled audit categories from the realm's
/// <c>UserAccountsRealmOptions.SecurityLifecycle.AuditCategories</c> via the options resolver. Replaces the module
/// default (all-on for every realm) once the integration is wired.
/// </summary>
public sealed class RealmSecurityAuditPolicyProvider(IUserAccountsRealmOptionsResolver optionsResolver)
    : ISecurityAuditPolicyProvider
{
    /// <inheritdoc />
    public SecurityAuditCategories GetCategories(string realmId)
        => optionsResolver.Resolve(realmId).SecurityLifecycle.AuditCategories;
}
