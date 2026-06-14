using System.Collections.Concurrent;
using RoyalIdentity.Users;
using RoyalIdentity.Users.Contracts;

namespace RoyalIdentity.Storage.InMemory;

/// <summary>
/// In-memory (fake/reference) <see cref="ISubjectStore"/>: looks up a lean <see cref="Subject"/> by its
/// stable <c>sub</c>. Realm is bound at construction (the users dictionary belongs to one realm). The fake
/// store scans by subject id; a real store (UsersAccounts module) would keep an index.
/// </summary>
public sealed class MemorySubjectStore(ConcurrentDictionary<string, UserDetails> users) : ISubjectStore
{
    public Task<Subject?> FindBySubjectIdAsync(string subjectId, CancellationToken ct = default)
    {
        var details = Find(subjectId);
        var subject = details is null ? null : new Subject(details.SubjectId, details.DisplayName, details.IsActive);
        return Task.FromResult(subject);
    }

    public Task<bool> IsActiveAsync(string subjectId, CancellationToken ct = default)
        => Task.FromResult(Find(subjectId) is { IsActive: true });

    private UserDetails? Find(string subjectId)
        => users.Values.FirstOrDefault(u => u.SubjectId == subjectId);
}
