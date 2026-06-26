// Ignore Spelling: Tls

namespace RoyalIdentity.Options;

/// <summary>
/// All options (configurations) for a realm.
/// </summary>
public class RealmOptions
{
    /// <summary>
    /// Creates a new instance of <see cref="RealmOptions"/>.
    /// </summary>
    /// <param name="serverOptions">The RoyalIdentity server options.</param>
    public RealmOptions(ServerOptions serverOptions)
    {
        ServerOptions = serverOptions;
        Authentication = new AuthenticationOptions(serverOptions.Authentication);
        Csp = new CspOptions(serverOptions.Csp);
        Cors = new CorsOptions(serverOptions.Cors);
        Discovery = new DiscoveryOptions(serverOptions.Discovery);
        Endpoints = new EndpointsOptions(serverOptions.Endpoints);
        MutualTls = new MutualTlsOptions(serverOptions.MutualTls);
        Keys = new KeyOptions(serverOptions.Keys);
        Logging = new LoggingOptions(serverOptions.Logging);
        InputLengthRestrictions = new InputLengthRestrictions(serverOptions.InputLengthRestrictions);
        AccessTokenJwtType = serverOptions.AccessTokenJwtType;
        EmitScopesAsSpaceDelimitedStringInJwt = serverOptions.EmitScopesAsSpaceDelimitedStringInJwt;
        DispatchEvents = serverOptions.DispatchEvents;
    }

    /// <summary>
    /// Creates a new independent copy of another <see cref="RealmOptions"/> instance.
    /// </summary>
    /// <param name="other">The realm options to copy.</param>
    public RealmOptions(RealmOptions other)
    {
        ServerOptions = other.ServerOptions;
        Discovery = new DiscoveryOptions(other.Discovery);
        UI = new RealmUIOptions(other.UI);
        Authentication = new AuthenticationOptions(other.Authentication);
        Csp = new CspOptions(other.Csp);
        Cors = new CorsOptions(other.Cors);
        Logging = new LoggingOptions(other.Logging);
        InputLengthRestrictions = new InputLengthRestrictions(other.InputLengthRestrictions);
        Endpoints = new EndpointsOptions(other.Endpoints);
        MutualTls = new MutualTlsOptions(other.MutualTls);
        Keys = new KeyOptions(other.Keys);
        Caching = new CacheOptions(other.Caching);
        Account = new AccountOptions(other.Account);
        Session = new SessionOptions(other.Session);
        Branding = new RealmBrandingOptions(other.Branding);
        IssuerUri = other.IssuerUri;
        LowerCaseIssuerUri = other.LowerCaseIssuerUri;
        IncludeRealmPathToIssuerUri = other.IncludeRealmPathToIssuerUri;
        StoreAuthorizationParameters = other.StoreAuthorizationParameters;
        AccessTokenJwtType = other.AccessTokenJwtType;
        EmitScopesAsSpaceDelimitedStringInJwt = other.EmitScopesAsSpaceDelimitedStringInJwt;
        DispatchEvents = other.DispatchEvents;
    }

    /// <summary>
    /// The RoyalIdentity server options.
    /// </summary>
    public ServerOptions ServerOptions { get; }

    /// <summary>
    /// Gets or sets the discovery options.
    /// </summary>
    public DiscoveryOptions Discovery { get; set; } = new();

    /// <summary>
    /// Gets or sets the UI options.
    /// </summary>
    public RealmUIOptions UI { get; set; } = new();

    /// <summary>
    /// Gets or sets the authentication options.
    /// </summary>
    public AuthenticationOptions Authentication { get; set; }

    /// <summary>
    /// Gets or sets the Content Security Policy options.
    /// </summary>
    public CspOptions Csp { get; set; }

    /// <summary>
    /// Gets or sets the CORS options.
    /// </summary>
    public CorsOptions Cors { get; set; }

    /// <summary>
    /// Gets or sets the logging options.
    /// </summary>
    public LoggingOptions Logging { get; set; }

    /// <summary>
    /// Gets or sets the max input length restrictions.
    /// </summary>
    public InputLengthRestrictions InputLengthRestrictions { get; set; }

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
    /// Gets or sets the Keys Options.
    /// </summary>
    public KeyOptions Keys { get; set; } = new();

    /// <summary>
    /// Gets or sets the caching options.
    /// </summary>
    public CacheOptions Caching { get; set; } = new();

    /// <summary>
    /// Gets or sets the account options.
    /// </summary>
    public AccountOptions Account { get; set; } = new();

    /// <summary>
    /// Gets or sets the SSO session lifetime and passive-invalidation policy (Realm-only — ADR-017 §2.12).
    /// </summary>
    public SessionOptions Session { get; set; } = new();

    /// <summary>
    /// Gets or sets the visual branding options for this realm.
    /// </summary>
    public RealmBrandingOptions Branding { get; set; } = new();

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

    /// <summary>
    /// Gets or sets whether the authorization parameters should be stored when authorize endpoint requires user interaction.
    /// </summary>
    public bool StoreAuthorizationParameters { get; set; } = true;

    /// <summary>
    /// Gets or sets the value for the JWT 'typ' header for access tokens.
    /// </summary>
    public string AccessTokenJwtType { get; set; }

    /// <summary>
    /// Specifies whether scopes in JWTs are emitted as array or string.
    /// </summary>
    public bool EmitScopesAsSpaceDelimitedStringInJwt { get; set; }

    /// <summary>
    /// Specifies whether events should be dispatched or not.
    /// </summary>
    public bool DispatchEvents { get; set; }
}
