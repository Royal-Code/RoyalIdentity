using RoyalIdentity.Models;
using RoyalIdentity.Users.Contracts;

namespace RoyalIdentity.Storage.InMemory;

/// <summary>
/// In-memory (fake/reference) <see cref="IUserDirectory"/> — the dedicated gateway to the realm-bound
/// account ports (Q1, ADR-014 §2.4). Each getter binds the realm at construction of the returned port, so
/// the ports take no realm parameter. Backed by <see cref="MemoryStorage"/>; the UserAccounts module
/// replaces it later (a DI swap), keeping <c>IStorage</c> free of account data.
/// </summary>
public sealed class MemoryUserDirectory(
    MemoryStorage storage,
    IPasswordProtector passwordProtector,
    TimeProvider clock) : IUserDirectory
{
    public ISubjectStore GetSubjectStore(Realm realm)
        => new MemorySubjectStore(storage.GetRealmMemoryStore(realm).UserAccounts);

    public ILocalUserAuthenticator GetLocalAuthenticator(Realm realm)
        => new MemoryLocalUserAuthenticator(
            storage.GetRealmMemoryStore(realm).UserAccounts,
            realm.Options.Account,
            passwordProtector,
            new LockoutPolicy(realm.Options.Account, clock));

    public IUserClaimsProvider GetClaimsProvider(Realm realm)
        => new MemoryUserClaimsProvider(storage.GetRealmMemoryStore(realm).UserAccounts);
}
