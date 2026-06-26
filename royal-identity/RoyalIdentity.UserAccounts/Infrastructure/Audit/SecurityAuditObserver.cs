using RoyalCode.DomainEvents;
using RoyalIdentity.UserAccounts.Features.Accounts.Domain;
using RoyalIdentity.UserAccounts.Infrastructure.Events;
using RoyalIdentity.UserAccounts.Options;

namespace RoyalIdentity.UserAccounts.Infrastructure.Audit;

/// <summary>
/// Maps committed security domain events to <see cref="SecurityAuditEntry"/> records and writes the ones whose
/// category is enabled for the realm (Q8). There is no anticipated catalog — each event is mapped as it appears; an
/// unmapped event is ignored. Entries never carry secrets (the raw action token never reaches an event).
/// </summary>
public sealed class SecurityAuditObserver(
    ISecurityAuditSink sink,
    ISecurityAuditPolicyProvider policyProvider) : IDomainEventObserver
{
    /// <inheritdoc />
    public async Task OnEventAsync(IDomainEvent domainEvent, CancellationToken ct = default)
    {
        if (!TryMap(domainEvent, out var entry))
        {
            return;
        }

        var enabled = policyProvider.GetCategories(entry.RealmId);
        if ((enabled & entry.Category) != entry.Category)
        {
            return;
        }

        await sink.WriteAsync(entry, ct);
    }

    private static bool TryMap(IDomainEvent domainEvent, out SecurityAuditEntry entry)
    {
        entry = domainEvent switch
        {
            // Route the password change by reason: a recovery reset is a Recovery event and an administrative reset is
            // an AdminSecurity event — collapsing both into Credential would misclassify them (ADR-017 §2.11).
            UserAccountPasswordChanged e =>
                Build(e.RealmId, e.SubjectId, CategoryFor(e.Reason), nameof(UserAccountPasswordChanged), e),
            UserAccountLocalCredentialLocked e =>
                Build(e.RealmId, e.SubjectId, SecurityAuditCategories.Lockout, nameof(UserAccountLocalCredentialLocked), e),
            UserAccountLocalCredentialUnlocked e =>
                Build(e.RealmId, e.SubjectId, SecurityAuditCategories.Lockout, nameof(UserAccountLocalCredentialUnlocked), e),
            UserAccountBlocked e =>
                Build(e.RealmId, e.SubjectId, SecurityAuditCategories.AdminSecurity, nameof(UserAccountBlocked), e),
            UserAccountUnblocked e =>
                Build(e.RealmId, e.SubjectId, SecurityAuditCategories.AdminSecurity, nameof(UserAccountUnblocked), e),
            // Role grants/revocations and activation/deactivation are authorization-state changes (AdminSecurity).
            UserAccountRoleAdded e =>
                Build(e.RealmId, e.SubjectId, SecurityAuditCategories.AdminSecurity, nameof(UserAccountRoleAdded), e),
            UserAccountRoleRemoved e =>
                Build(e.RealmId, e.SubjectId, SecurityAuditCategories.AdminSecurity, nameof(UserAccountRoleRemoved), e),
            UserAccountActivated e =>
                Build(e.RealmId, e.SubjectId, SecurityAuditCategories.AdminSecurity, nameof(UserAccountActivated), e),
            UserAccountDeactivated e =>
                Build(e.RealmId, e.SubjectId, SecurityAuditCategories.AdminSecurity, nameof(UserAccountDeactivated), e),
            UserAccountEmailVerified e =>
                Build(e.RealmId, e.SubjectId, SecurityAuditCategories.Verification, nameof(UserAccountEmailVerified), e),
            UserAccountPhoneVerified e =>
                Build(e.RealmId, e.SubjectId, SecurityAuditCategories.Verification, nameof(UserAccountPhoneVerified), e),
            _ => null
        };

        return entry is not null;
    }

    /// <summary>
    /// Maps a <see cref="PasswordChangeReason"/> to the most specific audit category. A single category per entry keeps
    /// the policy gate (which requires every bit of the entry category to be enabled) unambiguous.
    /// </summary>
    private static SecurityAuditCategories CategoryFor(PasswordChangeReason reason) => reason switch
    {
        PasswordChangeReason.Reset => SecurityAuditCategories.Recovery,
        PasswordChangeReason.AdminSet => SecurityAuditCategories.AdminSecurity,
        _ => SecurityAuditCategories.Credential // Create, Change, Import
    };

    private static SecurityAuditEntry Build(
        string realmId, string subjectId, SecurityAuditCategories category, string eventType, IDomainEvent domainEvent)
        => new(realmId, subjectId, category, eventType, domainEvent.Occurred);
}
