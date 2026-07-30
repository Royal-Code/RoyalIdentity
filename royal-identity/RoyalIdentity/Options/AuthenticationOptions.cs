using Microsoft.AspNetCore.Http;

namespace RoyalIdentity.Options;

/// <summary>
/// Configures the login and logout views and behavior.
/// </summary>
public class AuthenticationOptions
{
    /// <summary>
    /// Creates a new instance of <see cref="AuthenticationOptions"/>.
    /// </summary>
    public AuthenticationOptions()
    {
    }

    /// <summary>
    /// Creates a new instance of <see cref="AuthenticationOptions"/> copying values from another instance.
    /// </summary>
    /// <param name="other">The options to copy.</param>
    public AuthenticationOptions(AuthenticationOptions other)
    {
        CookieName = other.CookieName;
        CookieLifetime = other.CookieLifetime;
        CookieSlidingExpiration = other.CookieSlidingExpiration;
        CookieSameSiteMode = other.CookieSameSiteMode;
        CheckSessionCookieName = other.CheckSessionCookieName;
        CheckSessionCookieDomain = other.CheckSessionCookieDomain;
        CheckSessionCookieSameSiteMode = other.CheckSessionCookieSameSiteMode;
        RequireCspFrameSrcForSignOut = other.RequireCspFrameSrcForSignOut;
        AuthorizationInteractionLifetime = other.AuthorizationInteractionLifetime;
        ClientAssertionMaxLifetime = other.ClientAssertionMaxLifetime;
    }

    /// <summary>
    /// Gets or sets the cookie name used to persist the user's session details.
    /// </summary>
    public string CookieName { get; set; } = Server.DefaultCookieName;

    /// <summary>
    /// Sets the cookie lifetime.
    /// </summary>
    public TimeSpan CookieLifetime { get; set; } = Server.DefaultCookieTimeSpan;

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
    public string CheckSessionCookieName { get; set; } = Server.DefaultCheckSessionCookieName;

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

    /// <summary>
    /// <para>
    ///     Gets or sets how long, <b>in seconds</b>, a stored authorize request stays resumable while the user
    ///     goes through the login and consent screens. The unit follows the existing <c>Client</c> lifetimes
    ///     (plan-data-operational-storage DF40).
    /// </para>
    /// <para>
    ///     The authorize-parameters store writes the absolute expiration at store time, so changing this value
    ///     never reinterprets records that already exist; reading an expired record is fail-closed (DF16). It
    ///     only applies when <see cref="RealmOptions.StoreAuthorizationParameters"/> is on — otherwise the
    ///     parameters travel in the query string and no record is created.
    /// </para>
    /// </summary>
    public int AuthorizationInteractionLifetime { get; set; } = Server.DefaultAuthorizationInteractionLifetime;

    /// <summary>
    /// <para>
    ///     Gets or sets how far ahead of the server's own clock a <c>private_key_jwt</c> client assertion may
    ///     claim to expire (plan-replay-protection DF19/DF21). An assertion whose <c>exp</c> goes beyond
    ///     <c>now + ClientAssertionMaxLifetime</c> is refused as an invalid credential.
    /// </para>
    /// <para>
    ///     The ceiling is compared against the server's instant, not against <c>exp - iat</c>: <c>iat</c> is
    ///     optional and may arrive ahead, while what has to be bounded is the retention of the replay record —
    ///     a function of <c>exp</c> and the server's clock. Without it, a client would choose how long the
    ///     server retains its handles and how long a leaked assertion stays usable.
    /// </para>
    /// <para>
    ///     Accepted range: <see cref="Server.MinClientAssertionMaxLifetime"/> to
    ///     <see cref="Server.MaxClientAssertionMaxLifetime"/>, inclusive.
    /// </para>
    /// </summary>
    public TimeSpan ClientAssertionMaxLifetime { get; set; } = Server.DefaultClientAssertionMaxLifetime;

    /// <summary>
    /// Validates internal consistency of the authentication options.
    /// </summary>
    /// <returns>A list of configuration errors. Empty means valid.</returns>
    public IReadOnlyList<string> Validate()
    {
        List<string> errors = [];

        if (AuthorizationInteractionLifetime <= 0)
        {
            errors.Add("Authentication.AuthorizationInteractionLifetime must be greater than zero (seconds).");
        }

        if (ClientAssertionMaxLifetime < Server.MinClientAssertionMaxLifetime
            || ClientAssertionMaxLifetime > Server.MaxClientAssertionMaxLifetime)
        {
            errors.Add(
                "Authentication.ClientAssertionMaxLifetime must be between " +
                $"{Server.MinClientAssertionMaxLifetime} and {Server.MaxClientAssertionMaxLifetime}, inclusive.");
        }

        return errors;
    }
}
