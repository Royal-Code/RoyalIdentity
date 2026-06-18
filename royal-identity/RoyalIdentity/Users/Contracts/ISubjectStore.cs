namespace RoyalIdentity.Users.Contracts;

/// <summary>
/// Edge lookup of a subject by its stable <c>sub</c>. Realm is bound at construction
/// (factory / <see cref="IUserDirectory"/>), never passed per call. Backed in-memory now; by the
/// RoyalIdentity.UserAccounts module later (ADR-014 §2.3).
/// </summary>
public interface ISubjectStore
{
    /// <summary>Finds a subject by its stable subject id, or <c>null</c> when not found.</summary>
    Task<Subject?> FindBySubjectIdAsync(string subjectId, CancellationToken ct = default);

    /// <summary>Whether the account behind the subject id is active.</summary>
    Task<bool> IsActiveAsync(string subjectId, CancellationToken ct = default);
}
