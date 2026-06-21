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
    {
        // The fake uses default MemoryAccountOptions, so per-realm login-by-email and lockout policy are NOT honored
        // through this path (tests exercise non-default policy by constructing MemoryLocalUserAuthenticator directly).
        // The real per-realm path now exists in the module integration (plan-users-accounts-module-v2 Fase 9):
        // RoyalIdentity.UserAccounts.Integration.LocalUserAuthenticator drives login-by-email/lockout from the
        // realm's UserAccountsRealmOptions (resolved via UserAccountsRealmBinding). This fake stays minimal as the
        // reference implementation until Fase 10 swaps it; no active regression: no seeded realm configures these today.
        var options = new MemoryAccountOptions();

        return new MemoryLocalUserAuthenticator(
            storage.GetRealmMemoryStore(realm).UserAccounts,
            options,
            passwordProtector,
            new LockoutPolicy(options, clock));
    }

    public IUserClaimsProvider GetClaimsProvider(Realm realm)
        => new MemoryUserClaimsProvider(storage.GetRealmMemoryStore(realm).UserAccounts);
}
