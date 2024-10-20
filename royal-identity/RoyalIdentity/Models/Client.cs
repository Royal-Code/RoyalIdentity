﻿using RoyalIdentity.Options;
using System.Security.Claims;
using static RoyalIdentity.Options.OidcConstants;

namespace RoyalIdentity.Models;

/// <summary>
/// OAuth Client.
/// </summary>
public class Client
{
    /// <summary>
    /// The Client Id (client_id).
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// The client name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Determines whether the client is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the protocol type.
    /// </summary>
    /// <value>
    /// The protocol type.
    /// </value>
    public string ProtocolType { get; set; } = ServerConstants.ProtocolTypes.OpenIdConnect;

    /// <summary>
    /// Specifies whether a proof key is required for authorization code based token requests (defaults to <c>true</c>).
    /// </summary>
    public bool RequirePkce { get; set; } = true;

    /// <summary>
    /// Specifies whether a proof key can be sent using plain method (not recommended and defaults to <c>false</c>.)
    /// </summary>
    public bool AllowPlainTextPkce { get; set; } = false;

    /// <summary>
    /// Gets or sets a value indicating whether [allow offline access]. Defaults to <c>false</c>.
    /// </summary>
    public bool AllowOfflineAccess { get; set; } = false;

    /// <summary>
    /// Specifies the api scopes that the client is allowed to request. If empty, the client can't access any scope
    /// </summary>
    public HashSet<string> AllowedScopes { get; } = [];

    /// <summary>
    /// Specifies the response types that the client is allowed to request. If empty, the client can't access any scope
    /// </summary>
    public HashSet<string> AllowedResponseTypes { get; } = [ ResponseTypes.Code ];

    /// <summary>
    /// Signing algorithm for identity token. If empty, will use the server default signing algorithm.
    /// </summary>
    public HashSet<string> AllowedIdentityTokenSigningAlgorithms { get; } = [];

    /// <summary>
    /// Specifies which external IdPs can be used with this client (if list is empty all IdPs are allowed). Defaults to empty.
    /// </summary>
    public HashSet<string> IdentityProviderRestrictions { get; } = [];

    /// <summary>
    /// Allows settings claims for the client (will be included in the access token).
    /// </summary>
    /// <value>
    /// The claims.
    /// </value>
    public HashSet<Claim> Claims { get; } = [];

    /// <summary>
    /// Specifies allowed URIs to return tokens or authorization codes to
    /// </summary>
    public HashSet<string> RedirectUris { get; set; } = [];

    /// <summary>
    /// Specifies allowed URIs to redirect to after logout
    /// </summary>
    public HashSet<string> PostLogoutRedirectUris { get; set; } = [];

    /// <summary>
    /// Gets or sets a value indicating whether JWT access tokens should include an identifier. Defaults to <c>true</c>.
    /// </summary>
    /// <value>
    /// <c>true</c> to add an id; otherwise, <c>false</c>.
    /// </value>
    public bool IncludeJwtId { get; set; } = true;
    
    /// <summary>
    /// Gets or sets a value indicating whether client claims should be always included in the access tokens - or only for client credentials flow.
    /// Defaults to <c>true</c>
    /// </summary>
    /// <value>
    /// <c>true</c> if claims should always be sent; otherwise, <c>false</c>.
    /// </value>
    public bool AlwaysSendClientClaims { get; set; } = true;

    /// <summary>
    /// Gets or sets a value to prefix it on client claim types. Defaults to <c>null</c>.
    /// </summary>
    /// <value>
    /// Any non-empty string if claims should be prefixed with the value; otherwise, <c>null</c>.
    /// </value>
    public string? ClientClaimsPrefix { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the local login is allowed for this client. Defaults to <c>true</c>.
    /// </summary>
    /// <value>
    ///   <c>true</c> if local logins are enabled; otherwise, <c>false</c>.
    /// </value>
    public bool EnableLocalLogin { get; set; } = true;

    /// <summary>
    /// The maximum duration (in seconds) since the last time the user authenticated.
    /// </summary>
    public int? UserSsoLifetime { get; set; }

    /// <summary>
    /// Lifetime of access token in seconds (defaults to 600 seconds / 10 minutes)
    /// </summary>
    public int AccessTokenLifetime { get; set; } = 600;

    /// <summary>
    /// Lifetime of identity token in seconds (defaults to 600 seconds / 10 minutes)
    /// </summary>
    public int IdentityTokenLifetime { get; set; } = 600;

    /// <summary>
    /// Lifetime of authorization code in seconds (defaults to 60 seconds / 1 minutes)
    /// </summary>
    public int AuthorizationCodeLifetime { get; set; } = 60;

    /// <summary>
    /// Maximum lifetime of a refresh token in seconds. Defaults to 2.592.000 seconds / 30 days
    /// </summary>
    public int AbsoluteRefreshTokenLifetime { get; set; } = 2_592_000;

    /// <summary>
    /// Sliding lifetime of a refresh token in seconds. Defaults to 43.200 seconds / 12 hours
    /// </summary>
    public int SlidingRefreshTokenLifetime { get; set; } = 43_200;

    /// <summary>
    /// Lifetime of a user consent in seconds. Defaults to null (no expiration)
    /// </summary>
    public int? ConsentLifetime { get; set; } = null;

    /// <summary>
    /// Specifies whether a consent screen is required (defaults to <c>false</c>)
    /// </summary>
    public bool RequireConsent { get; set; } = false;

    /// <summary>
    /// Specifies whether user can choose to store consent decisions (defaults to <c>true</c>)
    /// </summary>
    public bool AllowRememberConsent { get; set; } = true;
}
