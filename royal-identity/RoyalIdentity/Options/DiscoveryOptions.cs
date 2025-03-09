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

    /// <summary>
    /// Gets or sets the supported response types.
    /// </summary>
    public HashSet<string> SupportedResponseTypes { get; } =
    [
        ResponseTypes.Code,
        ResponseTypes.Token,
        ResponseTypes.IdToken
    ];

    public HashSet<string> CodeChallengeMethodsSupported { get; } =
    [
        CodeChallengeMethods.Plain,
        CodeChallengeMethods.Sha256
    ];

    public HashSet<string> SupportedResponseModes { get; } =
    [
        ResponseModes.FormPost,
        ResponseModes.Fragment,
        ResponseModes.Query
    ];

    public HashSet<string> SupportedSubjectTypes { get; } =
    [
        SubjectTypes.Public
    ];

    public HashSet<string> SupportedDisplayModes { get; } =
    [
        DisplayModes.Page,
        DisplayModes.Popup,
        DisplayModes.Touch,
        DisplayModes.Wap
    ];

    public HashSet<string> SupportedPromptModes { get; } =
    [
        PromptModes.None,
        PromptModes.Login,
        PromptModes.Consent,
        PromptModes.SelectAccount
    ];

    public bool ResponseTypesIsSupported(ICollection<string> responseTypes)
    {
        return responseTypes.All(SupportedResponseTypes.Contains);
    }

    public bool CodeChallengeMethodIsSupported(string codeChallengeMethod)
    {
        return CodeChallengeMethodsSupported.Contains(codeChallengeMethod);
    }

    public bool ResponseModeIsSupported(string responseMode)
    {
        return SupportedResponseModes.Contains(responseMode);
    }

    public bool SubjectTypeIsSupported(string subjectType)
    {
        return SupportedSubjectTypes.Contains(subjectType);
    }

    public bool DisplayModeIsSupported(string displayMode)
    {
        return SupportedDisplayModes.Contains(displayMode);
    }

    public bool PromptModeIsSupported(string promptMode)
    {
        return SupportedPromptModes.Contains(promptMode);
    }
}