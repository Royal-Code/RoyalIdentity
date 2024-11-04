namespace RoyalIdentity.Options;

/// <summary>
/// All options for RoyalIdentity.
/// </summary>
public class ServerOptions
{
    /// <summary>
    /// Gets or sets the authentication options.
    /// </summary>
    /// <value>
    /// The authentication options.
    /// </value>
    public AuthenticationOptions Authentication { get; set; } = new();

    /// <summary>
    /// Gets or sets the max input length restrictions.
    /// </summary>
    /// <value>
    /// The length restrictions.
    /// </value>
    public InputLengthRestrictions InputLengthRestrictions { get; set; } = new();

    /// <summary>
    /// Gets or sets the options for the user interaction.
    /// </summary>
    /// <value>
    /// The user interaction options.
    /// </value>
    public UserInteractionOptions UserInteraction { get; set; } = new();

    /// <summary>
    /// Gets or sets the logging options
    /// </summary>
    public LoggingOptions Logging { get; set; } = new();

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
    public EndpointsOptions Endpoints { get; set; } = new EndpointsOptions();

    /// <summary>
    /// Gets or sets the Content Security Policy options.
    /// </summary>
    public CspOptions Csp { get; set; } = new();

    /// <summary>
    /// Gets or sets the mutual TLS options.
    /// </summary>
    public MutualTlsOptions MutualTls { get; set; } = new();

    /// <summary>
    /// Gets or sets the Keys Options options.
    /// </summary>
    public KeyOptions Keys { get; set; } = new();

    /// <summary>
    /// Gets or sets the unique name of this server instance, e.g. https://myissuer.com.
    /// If not set, the issuer name is inferred from the request
    /// </summary>
    /// <value>
    /// Unique name of this server instance, e.g. https://myissuer.com
    /// </value>
    public string? IssuerUri { get; set; }

    /// <summary>
    /// Set to false to preserve the original casing of the IssuerUri. Defaults to true.
    /// </summary>
    public bool LowerCaseIssuerUri { get; set; } = true;

    /// <summary>
    /// Gets or sets the value for the JWT typ header for access tokens.
    /// </summary>
    /// <value>
    /// The JWT typ value.
    /// </value>
    public string AccessTokenJwtType { get; set; } = "at+jwt";

    /// <summary>
    /// Specifies whether scopes in JWTs are emitted as array or string
    /// </summary>
    public bool EmitScopesAsSpaceDelimitedStringInJwt { get; set; } = false;

    /// <summary>
    /// Specifies whether events should be dispatched or not.
    /// </summary>
    public bool DispatchEvents { get; set; } = false;
}

