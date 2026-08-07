using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.DependencyInjection;
using RoyalIdentity.Contracts.Localization;
using RoyalIdentity.Extensions;
using RoyalIdentity.Models;
using RoyalIdentity.Users.Contracts;

namespace RoyalIdentity.Localization;

/// <summary>
/// Selects the UI culture of a request from the current realm's policy (plan-localization DF5): explicit
/// preference cookie, then a validated <c>ui_locales</c> hint, then <c>Accept-Language</c>, then the realm
/// default, then the catalogue's neutral locale.
/// </summary>
/// <remarks>
/// Everything here is a hint except the realm's own options: an unknown, malformed or unsupported value is
/// ignored and resolution continues down the chain (DF6). No path in this provider can fail a request.
/// </remarks>
public sealed class RealmRequestCultureProvider(IUiLocaleCatalog catalog) : RequestCultureProvider
{
    /// <inheritdoc />
    public override async Task<ProviderCultureResult?> DetermineProviderCultureResult(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        // No realm yet means this request never reached realm discovery — a static file, for instance. The
        // framework's remaining providers and the default culture handle it.
        if (!httpContext.TryGetCurrentRealm(out var realm))
            return null;

        var internationalization = realm.Options.Internationalization;
        var supported = internationalization.SupportedLocales;

        // Disabled means no negotiation at all: the realm renders in its default and advertises nothing.
        if (!internationalization.Enabled)
            return Result(internationalization.DefaultLocale);

        if (CulturePreferenceCookie.Read(httpContext, realm) is { } preferred)
            return Result(preferred);

        var uiLocales = await ResolveUiLocalesAsync(httpContext, realm);
        if (LocaleMatcher.TryMatchPreferenceList(uiLocales, supported, out var hinted))
            return Result(hinted);

        if (TryMatchAcceptLanguage(httpContext, supported, out var accepted))
            return Result(accepted);

        return Result(internationalization.DefaultLocale);
    }

    /// <summary>
    /// Reads the OIDC <c>ui_locales</c> hint. The authorize and end-session requests carry it inline; the
    /// account screens carry it in the <c>returnUrl</c>, which may in turn point at server-stored parameters,
    /// so the authorization context resolver is what reads it back (MP-5).
    /// </summary>
    private static async Task<string?> ResolveUiLocalesAsync(HttpContext httpContext, Realm realm)
    {
        if (httpContext.Request.Query.TryGetValue(Oidc.Authorize.Request.UiLocales, out var inline)
            && !string.IsNullOrWhiteSpace(inline))
        {
            return inline!;
        }

        if (!httpContext.Request.Query.TryGetValue("returnUrl", out var returnUrl)
            || string.IsNullOrWhiteSpace(returnUrl))
        {
            return null;
        }

        try
        {
            var resolver = httpContext.RequestServices.GetService<IAuthorizationContextResolver>();
            if (resolver is null)
                return null;

            var context = await resolver.ResolveAsync(returnUrl!, httpContext.RequestAborted);
            return context?.UiLocales;
        }
        catch (OperationCanceledException)
        {
            // A cancelled request has no culture to negotiate; the caller falls back to the realm default.
            return null;
        }
    }

    private static bool TryMatchAcceptLanguage(
        HttpContext httpContext,
        IReadOnlyList<string> supported,
        out string? matched)
    {
        matched = null;

        // The framework already parses and orders Accept-Language by quality; only the allowlist filter is ours.
        var languages = httpContext.Request.GetTypedHeaders().AcceptLanguage;
        if (languages is null || languages.Count is 0)
            return false;

        foreach (var language in languages.OrderByDescending(value => value.Quality ?? 1d))
        {
            if (LocaleMatcher.TryMatch(language.Value.Value, supported, out matched))
                return true;
        }

        return false;
    }

    private ProviderCultureResult Result(string locale)
    {
        // The neutral catalogue is the last resort: a realm may name a default the composed UI cannot render,
        // and rendering key names would be worse than rendering the neutral language.
        var effective = catalog.NeutralLocale is { } neutral && !catalog.Supports(locale)
            ? neutral
            : locale;

        return new ProviderCultureResult(effective, effective);
    }
}
