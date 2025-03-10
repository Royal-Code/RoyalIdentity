namespace RoyalIdentity.Options;

public class RealmUIOptions
{
    /// <summary>
    /// Gets or sets the login URL. If a local URL, the value must start with a leading slash.
    /// </summary>
    /// <value>
    /// The login URL.
    /// </value>
    public string LoginPath { get; set; } = UI.Routes.Login;

    /// <summary>
    /// Gets or sets the login return URL parameter.
    /// </summary>
    /// <value>
    /// The login return URL parameter.
    /// </value>
    public string LoginParameter { get; set; } = UI.Routes.Params.ReturnUrl;

    /// <summary>
    /// Gets or sets the logout URL. If a local URL, the value must start with a leading slash.
    /// </summary>
    /// <value>
    /// The logout URL.
    /// </value>
    public string LogoutPath { get; set; } = UI.Routes.Logout;

    /// <summary>
    /// Gets or sets the logging out URL. If a local URL, the value must start with a leading slash.
    /// </summary>
    /// <value>
    /// The logout URL.
    /// </value>
    public string LoggingOutPath { get; set; } = UI.Routes.LoggingOut;

    /// <summary>
    /// Gets or sets the logged out URL. If a local URL, the value must start with a leading slash.
    /// </summary>
    /// <value>
    /// The logout URL.
    /// </value>
    public string LoggedOutPath { get; set; } = UI.Routes.LoggedOut;

    /// <summary>
    /// Gets or sets the logout identifier parameter.
    /// </summary>
    /// <value>
    /// The logout identifier parameter.
    /// </value>
    public string LogoutParameter { get; set; } = UI.Routes.Params.LogoutId;

    /// <summary>
    /// Gets or sets the consent URL. If a local URL, the value must start with a leading slash.
    /// </summary>
    /// <value>
    /// The consent URL.
    /// </value>
    public string ConsentPath { get; set; } = UI.Routes.Consent;

    /// <summary>
    /// Gets or sets the consent return URL parameter.
    /// </summary>
    /// <value>
    /// The consent return URL parameter.
    /// </value>
    public string ConsentParameter { get; set; } = UI.Routes.Params.ReturnUrl;

    /// <summary>
    /// Gets or sets the device verification URL.  If a local URL, the value must start with a leading slash.
    /// </summary>
    /// <value>
    /// The device verification URL.
    /// </value>
    public string DeviceVerificationPath { get; set; } = UI.Routes.DeviceVerification;

    /// <summary>
    /// Gets or sets the device verification user code parameter.
    /// </summary>
    /// <value>
    /// The device verification user code parameter.
    /// </value>
    public string DeviceVerificationParameter { get; set; } = UI.Routes.Params.UserCode;
}
