using RoyalIdentity.Contracts.Storage;
using RoyalIdentity.Models;
using RoyalIdentity.Users.Contracts;

namespace RoyalIdentity.Storage.EntityFramework.Operational.Stores;

/// <summary>
/// <para>
///     Scoped entry point for the Operational stores implemented by this adapter (plan DF1). Every store is
///     realm-bound at creation: the adapter takes <c>realm.Id</c> here and the pure data project never sees a
///     live <see cref="Realm"/> (plan DF5).
/// </para>
/// <para>
///     Like <c>IConfigurationStoreFactory</c>, it deliberately does not implement <see cref="IStorage"/>: the
///     complete production gateway composes both families explicitly, and the default host stays in-memory
///     until Plano 4 (plan DF21).
/// </para>
/// </summary>
public interface IOperationalStoreFactory
{
    /// <summary>
    /// Creates the realm-bound access-token store. Reference tokens are always persisted; JWTs follow the
    /// realm's <c>JwtAccessTokenPersistence</c> (plan DF13/DF31).
    /// </summary>
    IAccessTokenStore GetAccessTokenStore(Realm realm);

    /// <summary>
    /// Creates the realm-bound refresh-token store. Its return type carries the MP-3 capability, so this
    /// adapter cannot produce a store without it and can never reach the transitional fallback (plan DF39).
    /// </summary>
    IOperationalRefreshTokenStore GetRefreshTokenStore(Realm realm);

    /// <summary>
    /// Creates the realm-bound authorization-code store, carrying the MP-2 capability under the same rule as
    /// above.
    /// </summary>
    IOperationalAuthorizationCodeStore GetAuthorizationCodeStore(Realm realm);

    /// <summary>Creates the realm-bound user consent store.</summary>
    IUserConsentStore GetUserConsentStore(Realm realm);

    /// <summary>Creates the realm-bound SSO session store.</summary>
    IUserSessionStore GetUserSessionStore(Realm realm);

    /// <summary>Creates the realm-bound authorize-parameters store (MP-5).</summary>
    IAuthorizeParametersStore GetAuthorizeParametersStore(Realm realm);
}
