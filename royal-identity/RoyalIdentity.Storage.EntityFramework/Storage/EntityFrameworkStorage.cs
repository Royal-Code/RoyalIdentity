using RoyalIdentity.Configuration;
using RoyalIdentity.Contracts.Storage;
using RoyalIdentity.Models;
using RoyalIdentity.Options;
using RoyalIdentity.Storage.EntityFramework.Configuration.Stores;
using RoyalIdentity.Storage.EntityFramework.Operational.Stores;
using RoyalIdentity.Users.Contracts;

namespace RoyalIdentity.Storage.EntityFramework.Storage;

/// <summary>
/// The complete EF gateway: Configuration and Operational composed behind one <see cref="IStorage"/>
/// (plan DF21/DF22). It is composed by the production Server and the SQLite Demo after Plano 4 Fase 3, and it
/// requires both families: there is no production mode with Operational EF and Configuration missing.
/// <para>
/// The two families keep their own <c>DbContext</c> and connection even when they point at the same database
/// (plan DF2), so nothing here coordinates a commit between them: this type is composition, not a unit of work.
/// </para>
/// </summary>
internal sealed class EntityFrameworkStorage(
    IConfigurationSnapshot snapshot,
    IConfigurationStoreFactory configuration,
    IOperationalStoreFactory operational) : IStorage
{
    /// <summary>
    /// From the published snapshot, so the synchronous member of the contract performs no I/O and opens no
    /// connection (plan DF22). The snapshot already returns a defensive copy.
    /// </summary>
    public ServerOptions ServerOptions => snapshot.ServerOptions;

    public IRealmStore Realms => configuration.Realms;

    public IClientStore GetClientStore(Realm realm) => configuration.GetClientStore(realm);

    public IKeyStore GetKeyStore(Realm realm) => configuration.GetKeyStore(realm);

    /// <summary>Still the transitional volatile bridge: persisting resources waits on the redesign (B-DF22).</summary>
    public IResourceStore GetResourceStore(Realm realm) => configuration.GetResourceStore(realm);

    public IAccessTokenStore GetAccessTokenStore(Realm realm) => operational.GetAccessTokenStore(realm);

    public IRefreshTokenStore GetRefreshTokenStore(Realm realm) => operational.GetRefreshTokenStore(realm);

    public IAuthorizationCodeStore GetAuthorizationCodeStore(Realm realm)
        => operational.GetAuthorizationCodeStore(realm);

    public IUserConsentStore GetUserConsentStore(Realm realm) => operational.GetUserConsentStore(realm);

    public IUserSessionStore GetUserSessionStore(Realm realm) => operational.GetUserSessionStore(realm);

    public IAuthorizeParametersStore GetAuthorizeParametersStore(Realm realm)
        => operational.GetAuthorizeParametersStore(realm);
}
