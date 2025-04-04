﻿using RoyalIdentity.Models;
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
    /// Gets the access token store.
    /// </summary>
    /// <value>The access token store.</value>
    IAccessTokenStore AccessTokens { get; }

    /// <summary>
    /// Gets the refresh token store.
    /// </summary>
    /// <value>The refresh token store.</value>
    IRefreshTokenStore RefreshTokens { get; }

    /// <summary>
    /// Gets the authorization code store.
    /// </summary>
    /// <value>The authorization code store.</value>
    IAuthorizationCodeStore AuthorizationCodes { get; }

    /// <summary>
    /// Gets the authorize parameters store.
    /// </summary>
    /// <value>The authorize parameters store.</value>
    IAuthorizeParametersStore AuthorizeParameters { get; }

    /// <summary>
    /// Gets the user consents store.
    /// </summary>
    /// <value>The user consents store.</value>
    IUserConsentStore UserConsents { get; }

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
    /// Gets the user store for the given realm.
    /// </summary>
    /// <param name="realm">The realm.</param>
    /// <returns>The user store.</returns>
    IUserStore GetUserStore(Realm realm);

    /// <summary>
    /// Gets the user session store for the given realm.
    /// </summary>
    /// <param name="realm">The realm.</param>
    /// <returns>The user session store.</returns>
    IUserSessionStore GetUserSessionStore(Realm realm);

    /// <summary>
    /// Gets the user details store for the given realm.
    /// </summary>
    /// <param name="realm">The realm.</param>
    /// <returns>The user details store.</returns>
    IUserDetailsStore GetUserDetailsStore(Realm realm);
}
