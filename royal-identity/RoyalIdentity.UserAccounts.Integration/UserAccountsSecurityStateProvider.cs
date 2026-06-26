using RoyalIdentity.UserAccounts.Features.Accounts.Commons;
using RoyalIdentity.Users;
using RoyalIdentity.Users.Contracts;

namespace RoyalIdentity.UserAccounts.Integration;

/// <summary>
/// Module-backed <see cref="IUserSecurityStateProvider"/> (ADR-017 §2.10/§2.13, Q15): exposes the account
/// <c>SecurityStamp</c> (always, for sign-in capture) and the <c>SessionsValidAfter</c> marker. The marker is
/// returned <b>only when the realm enforces passive invalidation</b> (<c>RealmOptions.Session.EnableSessionInvalidationByState</c>);
/// otherwise it is <c>null</c> so the core does not enforce it. The realm is bound at construction.
/// </summary>
public sealed class UserAccountsSecurityStateProvider(
    UserAccountReader reader,
    string realmId,
    bool enforceSessionsValidAfter) : IUserSecurityStateProvider
{
    /// <inheritdoc />
    public async Task<UserSecurityState?> GetSecurityStateAsync(string subjectId, CancellationToken ct = default)
    {
        var account = await reader.FindBySubjectIdAsync(realmId, subjectId, ct);
        if (account is null)
        {
            return null;
        }

        // Gate the invalidation marker by the realm policy: the core treats a non-null value as "enforce".
        DateTimeOffset? sessionsValidAfter = enforceSessionsValidAfter ? account.SessionsValidAfter : null;

        return new UserSecurityState(account.SecurityStamp.Value, sessionsValidAfter);
    }
}
