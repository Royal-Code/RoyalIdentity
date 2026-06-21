using RoyalIdentity.UserAccounts.Features.Accounts.Commons;
using RoyalIdentity.Users;
using RoyalIdentity.Users.Contracts;

namespace RoyalIdentity.UserAccounts.Integration;

/// <summary>
/// Module-backed <see cref="ISubjectStore"/> (ADR-015 §2.1): looks up a lean edge <see cref="Subject"/> by its
/// stable <c>sub</c>. The realm is bound at construction (the adapter passes <see cref="realmId"/>), so the port
/// takes no realm parameter. Only the protocol projection (<c>sub</c>, <c>name</c>, active) crosses the edge —
/// the physical <c>UserAccount.Id</c> never leaks.
/// </summary>
public sealed class SubjectStore(UserAccountReader reader, string realmId) : ISubjectStore
{
    /// <inheritdoc />
    public async Task<Subject?> FindBySubjectIdAsync(string subjectId, CancellationToken ct = default)
    {
        var account = await reader.FindBySubjectIdAsync(realmId, subjectId, ct);
        return account is null
            ? null
            : new Subject(account.SubjectId, account.DisplayName, account.IsActive);
    }

    /// <inheritdoc />
    public async Task<bool> IsActiveAsync(string subjectId, CancellationToken ct = default)
    {
        var account = await reader.FindBySubjectIdAsync(realmId, subjectId, ct);
        return account is { IsActive: true };
    }
}
