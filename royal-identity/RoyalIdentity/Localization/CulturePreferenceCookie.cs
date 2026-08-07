using Microsoft.AspNetCore.Http;
using RoyalIdentity.Models;

namespace RoyalIdentity.Localization;

/// <summary>
/// The realm-scoped cookie that carries an explicit language choice (plan-localization DF10).
/// </summary>
/// <remarks>
/// Name and path are derived from the realm so two realms in the same browser never share a preference, and
/// the value is only ever a canonical locale tag — never a return URL or any other caller-controlled string.
/// </remarks>
public static class CulturePreferenceCookie
{
    private const string Prefix = ".roid.culture.";

    /// <summary>How long an explicit choice is remembered.</summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromDays(365);

    public static string GetName(Realm realm)
    {
        ArgumentNullException.ThrowIfNull(realm);
        return $"{Prefix}{realm.Path}";
    }

    public static string GetPath(Realm realm)
    {
        ArgumentNullException.ThrowIfNull(realm);
        return $"/{realm.Path}";
    }

    public static CookieOptions CreateOptions(Realm realm, DateTimeOffset now) => new()
    {
        // HttpOnly: no script needs to read it, and it is not a client-side feature flag.
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.Lax,
        IsEssential = true,
        Path = GetPath(realm),
        Expires = now.Add(Lifetime),
    };

    /// <summary>
    /// Reads the stored preference, accepting it only when the realm still offers that locale. A realm that
    /// drops a locale after a configuration refresh therefore stops honouring stale cookies immediately.
    /// </summary>
    public static string? Read(HttpContext httpContext, Realm realm)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(realm);

        var stored = httpContext.Request.Cookies[GetName(realm)];
        if (string.IsNullOrWhiteSpace(stored))
            return null;

        return LocaleMatcher.TryMatch(stored, realm.Options.Internationalization.SupportedLocales, out var matched)
            ? matched
            : null;
    }
}
