// Ignore Spelling: Api

namespace RoyalIdentity.Options;

/// <summary>
/// Options class to configure discovery endpoint
/// </summary>
public class DiscoveryOptions
{
    /// <summary>
    /// Show endpoints
    /// </summary>
    public bool ShowEndpoints { get; set; } = true;

    /// <summary>
    /// Show signing keys
    /// </summary>
    public bool ShowKeySet { get; set; } = true;

    /// <summary>
    /// Show identity scopes
    /// </summary>
    public bool ShowIdentityScopes { get; set; } = true;

    /// <summary>
    /// Show API scopes
    /// </summary>
    public bool ShowApiScopes { get; set; } = true;

    /// <summary>
    /// Show identity claims
    /// </summary>
    public bool ShowClaims { get; set; } = true;

    /// <summary>
    /// Show response types
    /// </summary>
    public bool ShowResponseTypes { get; set; } = true;

    /// <summary>
    /// Show response modes
    /// </summary>
    public bool ShowResponseModes { get; set; } = true;

    /// <summary>
    /// Show standard grant types
    /// </summary>
    public bool ShowGrantTypes { get; set; } = true;

    /// <summary>
    /// Show custom grant types
    /// </summary>
    public bool ShowExtensionGrantTypes { get; set; } = true;

    /// <summary>
    /// Show token endpoint authentication methods
    /// </summary>
    public bool ShowTokenEndpointAuthenticationMethods { get; set; } = true;

    /// <summary>
    /// Turns relative paths that start with ~/ into absolute paths
    /// </summary>
    public bool ExpandRelativePathsInCustomEntries { get; set; } = true;

    /// <summary>
    /// Sets the max age value of the cache control header (in seconds) of the HTTP response.
    /// <br />
    /// This gives clients a hint how often they should refresh their cached copy of the discovery document.
    /// <br />
    /// If set to 0 no-cache headers will be set. 
    /// <br />
    /// Defaults to null, which does not set the header.
    /// </summary>
    /// <value>
    /// The cache interval in seconds.
    /// </value>
    public int? ResponseCacheInterval { get; set; } = null;

    /// <summary>
    /// Adds custom entries to the discovery document
    /// </summary>
    public Dictionary<string, object> CustomEntries { get; set; } = new();
}