using RoyalIdentity.Models;
using RoyalIdentity.Options;
using RoyalIdentity.Users.Contracts;

namespace RoyalIdentity.Contracts.Storage;

/// <summary>
/// Represents a storage for
/// </summary>
public interface IStorage
{
    /// <summary>
    /// Gets the server options.
    /// </summary>
    /// <value>The server options.</returns>
    ServerOptions ServerOptions { get; }

    /// <summary>
    /// Gets the realm store.
    /// </summary>
    /// <value>The realm store.</value>
    IRealmStore Realms { get; }

    /// <summary>
    /// Gets the authorize parameters store.
    /// </summary>
    /// <value>The authorize parameters store.</value>
    IAuthorizeParametersStore AuthorizeParameters { get; }

    /// <summary>
    /// Gets the access token store for the given realm.
    /// </summary>
    IAccessTokenStore GetAccessTokenStore(Realm realm);

    /// <summary>
    /// Gets the refresh token store for the given realm.
    /// </summary>
    IRefreshTokenStore GetRefreshTokenStore(Realm realm);

    /// <summary>
    /// Gets the authorization code store for the given realm.
    /// </summary>
    IAuthorizationCodeStore GetAuthorizationCodeStore(Realm realm);

    /// <summary>
    /// Gets the user consent store for the given realm.
    /// </summary>
    IUserConsentStore GetUserConsentStore(Realm realm);

    /// <summary>
    /// Gets the key store for the given realm.
    /// </summary>
    /// <param name="realm">The realm.</param>
    /// <returns>The key store.</returns>
    IKeyStore GetKeyStore(Realm realm);

    /// <summary>
    /// Gets the client store for the given realm.
    /// </summary>
    /// <param name="realm">The realm.</param>
    /// <returns>The client store.</returns>
    IClientStore GetClientStore(Realm realm);

    /// <summary>
    /// Gets the resource store for the given realm.
    /// </summary>
    /// <param name="realm">The realm.</param>
    /// <returns>The resource store.</returns>
    IResourceStore GetResourceStore(Realm realm);

    /// <summary>
    /// Gets the user session store for the given realm.
    /// </summary>
    /// <param name="realm">The realm.</param>
    /// <returns>The user session store.</returns>
    IUserSessionStore GetUserSessionStore(Realm realm);
}
