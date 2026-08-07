using Microsoft.AspNetCore.Http;
using RoyalIdentity.Extensions;

namespace RoyalIdentity.Localization;

/// <summary>
/// Applies an explicit language choice for the current realm (plan-localization DF10).
/// </summary>
public interface ICulturePreferenceService
{
    /// <summary>
    /// Stores <paramref name="locale"/> as this realm's preference when the realm offers it.
    /// </summary>
    /// <returns>The canonical locale stored, or <see langword="null"/> when the choice was refused.</returns>
    string? Apply(string? locale);

    /// <summary>
    /// The locales the current realm can offer a chooser, in configured order. Empty when the realm offers
    /// fewer than two, since a one-item selector is not a choice.
    /// </summary>
    IReadOnlyList<string> GetSelectableLocales();
}

/// <inheritdoc />
public sealed class CulturePreferenceService(
    IHttpContextAccessor httpContextAccessor,
    TimeProvider clock) : ICulturePreferenceService
{
    /// <inheritdoc />
    public string? Apply(string? locale)
    {
        if (!TryGetRealm(out var httpContext, out var realm))
            return null;

        var internationalization = realm.Options.Internationalization;
        if (!internationalization.Enabled)
            return null;

        // Only a locale the realm offers is ever written, and only in its canonical form: the cookie must not
        // become a channel for caller-controlled text.
        if (!LocaleMatcher.TryMatch(locale, internationalization.SupportedLocales, out var canonical))
            return null;

        httpContext.Response.Cookies.Append(
            CulturePreferenceCookie.GetName(realm),
            canonical,
            CulturePreferenceCookie.CreateOptions(realm, clock.GetUtcNow()));

        return canonical;
    }

    /// <inheritdoc />
    public IReadOnlyList<string> GetSelectableLocales()
    {
        if (!TryGetRealm(out _, out var realm))
            return [];

        var internationalization = realm.Options.Internationalization;

        return internationalization.Enabled && internationalization.SupportedLocales.Count > 1
            ? internationalization.SupportedLocales
            : [];
    }

    private bool TryGetRealm(
        out HttpContext httpContext,
        out RoyalIdentity.Models.Realm realm)
    {
        httpContext = httpContextAccessor.HttpContext!;
        realm = null!;

        return httpContext is not null && httpContext.TryGetCurrentRealm(out realm!);
    }
}
