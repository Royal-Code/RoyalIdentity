namespace RoyalIdentity.Options;

/// <summary>
/// Options for of the user interface endpoints.
/// </summary>
public class ServerUIOptions
{
    public ServerUIOptions()
    {
    }

    /// <summary>Creates an independent copy of another <see cref="ServerUIOptions"/> instance.</summary>
    public ServerUIOptions(ServerUIOptions other)
    {
        SelectDomainPath = other.SelectDomainPath;
        AccessDeniedPath = other.AccessDeniedPath;
        ErrorPath = other.ErrorPath;
        ErrorParameter = other.ErrorParameter;
        CustomRedirectParameter = other.CustomRedirectParameter;
    }

    /// <summary>
    /// Gets or sets the select domain URL. If a local URL, the value must start with a leading slash.
    /// </summary>
    public string SelectDomainPath { get; set; } = UI.Routes.SelectDomain;

    /// <summary>
    /// Gets or sets the access denied URL. If a local URL, the value must start with a leading slash.
    /// </summary>
    /// <value>
    /// The access denied URL.
    /// </value>
    public string AccessDeniedPath { get; set; } = UI.Routes.AccessDenied;

    /// <summary>
    /// Gets or sets the error URL. If a local URL, the value must start with a leading slash.
    /// </summary>
    /// <value>
    /// The error URL.
    /// </value>
    public string ErrorPath { get; set; } = UI.Routes.Error;

    /// <summary>
    /// Gets or sets the error identifier parameter.
    /// </summary>
    /// <value>
    /// The error identifier parameter.
    /// </value>
    public string ErrorParameter { get; set; } = UI.Routes.Params.ErrorId;

    /// <summary>
    /// Gets or sets the custom redirect return URL parameter.
    /// </summary>
    /// <value>
    /// The custom redirect return URL parameter.
    /// </value>
    public string CustomRedirectParameter { get; set; } = UI.Routes.Params.ReturnUrl;
}
