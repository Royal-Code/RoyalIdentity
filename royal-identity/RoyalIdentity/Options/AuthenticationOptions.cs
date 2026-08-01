// This file contains material derived from IdentityServer4 and/or IdentityModel.
// Original component copyrights remain with Brock Allen, Dominick Baier, and/or Duende Software.
// Licensed under Apache License 2.0; see LICENSES/Apache-2.0.txt and THIRD-PARTY-NOTICES.md.
// Modified by RoyalIdentity contributors for the RoyalIdentity rearchitecture.
//
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
        List<string> errors = [.. ValidateCheckSessionCookie()];

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

    internal IReadOnlyList<string> ValidateCheckSessionCookie()
    {
        List<string> errors = [];

        if (!CookieNameValidation.IsValid(CheckSessionCookieName))
        {
            errors.Add(
                "Authentication.CheckSessionCookieName must be a non-empty ASCII cookie token without " +
                "control characters or separators.");
        }

        if (string.Equals(CheckSessionCookieName, CookieName, StringComparison.Ordinal))
        {
            errors.Add(
                "Authentication.CheckSessionCookieName must differ from Authentication.CookieName to avoid " +
                "a predictable realm cookie collision.");
        }

        return errors;
    }
}

internal static class CookieNameValidation
{
    private const string Separators = "()<>@,;:\\\"/[]?={} ";

    internal static bool IsValid(string? value)
        => !string.IsNullOrWhiteSpace(value)
            && value.All(character => character is >= (char)0x21 and <= (char)0x7e
                && !Separators.Contains(character));
}
