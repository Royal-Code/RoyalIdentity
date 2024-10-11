using RoyalIdentity.Options;

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
    public string Name { get; internal set; }

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
    /// Specifies the allowed grant types (legal combinations of AuthorizationCode, Implicit, Hybrid, ResourceOwner, ClientCredentials).
    /// </summary>
    public ICollection<string> AllowedGrantTypes { get; set; } = [];

    /// <summary>
    /// Controls whether access tokens are transmitted via the browser for this client (defaults to <c>false</c>).
    /// This can prevent accidental leakage of access tokens when multiple response types are allowed.
    /// </summary>
    /// <value>
    /// <c>true</c> if access tokens can be transmitted via the browser; otherwise, <c>false</c>.
    /// </value>
    public bool AllowAccessTokensViaBrowser { get; set; } = false;

    /// <summary>
    /// Gets or sets a value indicating whether [allow offline access]. Defaults to <c>false</c>.
    /// </summary>
    public bool AllowOfflineAccess { get; set; } = false;

    /// <summary>
    /// Specifies the api scopes that the client is allowed to request. If empty, the client can't access any scope
    /// </summary>
    public ICollection<string> AllowedScopes { get; set; } = [];

    /// <summary>
    /// Gets or sets a value indicating whether the local login is allowed for this client. Defaults to <c>true</c>.
    /// </summary>
    /// <value>
    ///   <c>true</c> if local logins are enabled; otherwise, <c>false</c>.
    /// </value>
    public bool EnableLocalLogin { get; set; } = true;

    /// <summary>
    /// Specifies which external IdPs can be used with this client (if list is empty all IdPs are allowed). Defaults to empty.
    /// </summary>
    public ICollection<string> IdentityProviderRestrictions { get; set; } = [];

    /// <summary>
    /// The maximum duration (in seconds) since the last time the user authenticated.
    /// </summary>
    public int? UserSsoLifetime { get; set; }

    /// <summary>
    /// Signing algorithm for identity token. If empty, will use the server default signing algorithm.
    /// </summary>
    public ICollection<string> AllowedIdentityTokenSigningAlgorithms { get; set; } = [];

    /// <summary>
    /// Lifetime of access token in seconds (defaults to 3600 seconds / 1 hour)
    /// </summary>
    public int AccessTokenLifetime { get; set; } = 3600;

    /// <summary>
    /// Lifetime of authorization code in seconds (defaults to 300 seconds / 5 minutes)
    /// </summary>
    public int AuthorizationCodeLifetime { get; set; } = 300;

    /// <summary>
    /// Maximum lifetime of a refresh token in seconds. Defaults to 2592000 seconds / 30 days
    /// </summary>
    public int AbsoluteRefreshTokenLifetime { get; set; } = 2592000;

    /// <summary>
    /// Sliding lifetime of a refresh token in seconds. Defaults to 1296000 seconds / 15 days
    /// </summary>
    public int SlidingRefreshTokenLifetime { get; set; } = 1296000;

    /// <summary>
    /// Lifetime of a user consent in seconds. Defaults to null (no expiration)
    /// </summary>
    public int? ConsentLifetime { get; set; } = null;
}
