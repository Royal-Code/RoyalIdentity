using Microsoft.AspNetCore.Http;

namespace RoyalIdentity.Options;

/// <summary>
/// Configures the login and logout views and behavior.
/// </summary>
public class AuthenticationOptions
{
    /// <summary>
    /// Gets or sets the cookie name used to persist the user's session details.
    /// </summary>
    public string CookieName { get; set; } = ServerConstants.DefaultCookieName;

    /// <summary>
    /// Sets the cookie lifetime.
    /// </summary>
    public TimeSpan CookieLifetime { get; set; } = ServerConstants.DefaultCookieTimeSpan;

    /// <summary>
    /// Specified if the cookie should be sliding or not (only effective if the built-in cookie middleware is used)
    /// </summary>
    public bool CookieSlidingExpiration { get; set; } = true;

    /// <summary>
    /// Specifies the SameSite mode for the internal authentication and temp cookie
    /// </summary>
    public SameSiteMode CookieSameSiteMode { get; set; } = SameSiteMode.None;

    /// <summary>
    /// Gets or sets the name of the cookie used for the check session endpoint.
    /// </summary>
    public string CheckSessionCookieName { get; set; } = ServerConstants.DefaultCheckSessionCookieName;

    /// <summary>
    /// Gets or sets the domain of the cookie used for the check session endpoint. Defaults to null.
    /// </summary>
    [Redesign("Check in IS4 if this is still needed")]
    public string? CheckSessionCookieDomain { get; set; }

    /// <summary>
    /// Gets or sets the SameSite mode of the cookie used for the check session endpoint. Defaults to SameSiteMode.None.
    /// </summary>
    public SameSiteMode CheckSessionCookieSameSiteMode { get; set; } = SameSiteMode.None;

    /// <summary>
    /// If set, will require frame-src CSP headers being emitting on the end session callback endpoint which renders iframes to clients for front-channel sign out notification.
    /// </summary>
    public bool RequireCspFrameSrcForSignOut { get; set; } = true;
}