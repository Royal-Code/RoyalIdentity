using RoyalIdentity.Models;

namespace RoyalIdentity.Users.Contracts;

/// <summary>
/// Dedicated gateway to the realm-bound account ports (Q1, ADR-014 §2.4). Keeps account data out of
/// <c>IStorage</c> (which holds only IdP data, incl. session). Backed in-memory now; by the
/// RoyalIdentity.UserAccounts module later — swapping is a DI registration. Each getter binds the realm
/// at construction, so the returned ports take no realm parameter.
/// </summary>
public interface IUserDirectory
{
    /// <summary>
    /// Gets the subject lookup store bound to the realm.
    /// </summary>
    ISubjectStore GetSubjectStore(Realm realm);

    /// <summary>
    /// Gets the local authenticator bound to the realm.
    /// </summary>
    ILocalUserAuthenticator GetLocalAuthenticator(Realm realm);

    /// <summary>
    /// Gets the property→claims provider bound to the realm.
    /// </summary>
    IUserClaimsProvider GetClaimsProvider(Realm realm);

    /// <summary>
    /// Gets the optional security-state capability bound to the realm (Q15), or <c>null</c> when the user provider
    /// has no such state. The IdP uses it to capture the security stamp at sign-in and enforce passive session
    /// invalidation by <c>SessionsValidAfter</c>; when absent the IdP degrades gracefully — unless the realm requires
    /// it (<c>RealmOptions.Session.RequiresSecurityStateProvider</c>), which is a composition error detected
    /// fail-fast during session validation.
    /// </summary>
    IUserSecurityStateProvider? GetSecurityStateProvider(Realm realm);
}
