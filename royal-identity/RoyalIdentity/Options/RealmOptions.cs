// Ignore Spelling: Tls

namespace RoyalIdentity.Options;

/// <summary>
/// All options (configurations) for a realm.
/// </summary>
public class RealmOptions
{
    /// <summary>
    /// Gets or sets the discovery options.
    /// </summary>
    public DiscoveryOptions Discovery { get; set; } = new();

    /// <summary>
    /// Gets or sets the endpoint configuration.
    /// </summary>
    /// <value>
    /// The endpoints configuration.
    /// </value>
    public EndpointsOptions Endpoints { get; set; } = new();

    /// <summary>
    /// Gets or sets the mutual TLS options.
    /// </summary>
    public MutualTlsOptions MutualTls { get; set; } = new();

    /// <summary>
    /// Gets or sets the unique name of this server/realm instance, e.g. https://myissuer.com or https://myissuer.com/myrealm.
    /// If not set, the issuer name is inferred from the request
    /// </summary>
    /// <value>
    /// Unique name of this server/realm instance, e.g. https://myissuer.com or https://myissuer.com/myrealm.
    /// </value>
    public string? IssuerUri { get; set; }

    /// <summary>
    /// Set to false to preserve the original casing of the IssuerUri. Defaults to true.
    /// </summary>
    public bool LowerCaseIssuerUri { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether the realm path should be included in the issuer URI. Defaults to true.
    /// </summary>
    public bool IncludeRealmPathToIssuerUri { get; set; } = true;
}
