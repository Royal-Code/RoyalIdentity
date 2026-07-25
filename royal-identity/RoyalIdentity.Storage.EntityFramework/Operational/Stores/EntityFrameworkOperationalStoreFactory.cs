using RoyalIdentity.Contracts.Storage;
using RoyalIdentity.Models;
using RoyalIdentity.Storage.EntityFramework.Operational.Materialization;
using RoyalIdentity.Storage.EntityFramework.Operational.Protection;
using RoyalIdentity.Users.Contracts;

namespace RoyalIdentity.Storage.EntityFramework.Operational.Stores;

/// <summary>
/// Scoped factory of the realm-bound Operational stores. It takes the live <see cref="Realm"/> here — the pure
/// data project never sees one (plan DF5) — and hands each store the realm it is bound to, which is also how a
/// store resolves the realm's payload protection profile and JWT persistence mode.
/// <para>
/// The stores are delivered per phase, in the order the plan fixes. A member whose store has not landed yet
/// throws instead of returning a partial implementation; the complete production gateway is only composed once
/// they all exist (plan DF21), so nothing in production can reach one of these.
/// </para>
/// </summary>
internal sealed class EntityFrameworkOperationalStoreFactory(
    IOperationalDbContextAccessor accessor,
    OperationalLookupDigest digest,
    AccessTokenPayloadSerializer accessTokenSerializer,
    ConsentPayloadSerializer consentSerializer,
    OperationalPayloadProtection protection) : IOperationalStoreFactory
{
    public IAccessTokenStore GetAccessTokenStore(Realm realm)
    {
        ArgumentNullException.ThrowIfNull(realm);
        return new EntityFrameworkAccessTokenStore(realm, accessor, digest, accessTokenSerializer, protection);
    }

    public IUserConsentStore GetUserConsentStore(Realm realm)
    {
        ArgumentNullException.ThrowIfNull(realm);
        return new EntityFrameworkUserConsentStore(realm, accessor, consentSerializer, protection);
    }

    public IUserSessionStore GetUserSessionStore(Realm realm) => throw NotYetImplemented("SSO sessions", 3);

    public IOperationalAuthorizationCodeStore GetAuthorizationCodeStore(Realm realm)
        => throw NotYetImplemented("authorization codes", 4);

    public IOperationalRefreshTokenStore GetRefreshTokenStore(Realm realm)
        => throw NotYetImplemented("refresh tokens", 5);

    public IAuthorizeParametersStore GetAuthorizeParametersStore(Realm realm)
        => throw NotYetImplemented("authorize parameters", 6);

    private static NotSupportedException NotYetImplemented(string store, int phase)
        => new($"The Operational EF store for {store} lands in Fase {phase} of plan-data-operational-storage.");
}
