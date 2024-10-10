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
}
