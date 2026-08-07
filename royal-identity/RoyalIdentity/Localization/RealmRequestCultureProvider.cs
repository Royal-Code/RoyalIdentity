using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.DependencyInjection;
using RoyalIdentity.Contracts.Localization;
using RoyalIdentity.Contracts.Models.Messages;
using RoyalIdentity.Contracts.Storage;
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

        // Screens that choose the realm run before realm discovery can know one — domain selection is the
        // whole point. They still belong to the product, so they negotiate against the shipped catalogue: a
        // user who cannot read English must not be asked to pick a domain in English (decisão do mantenedor,
        // que resolve a contradição entre DF5 "sem realm ⇒ neutro" e o aceite da Fase 5).
        if (!httpContext.TryGetCurrentRealm(out var realm))
            return DetermineCatalogueCulture(httpContext);

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
    /// Reads the OIDC <c>ui_locales</c> hint from whichever carrier this request uses: inline on authorize and
    /// end-session, inside the <c>returnUrl</c> on the account screens — which may itself point at
    /// server-stored parameters (MP-5) — or inside the protected <c>LogoutMessage</c> on the logout screens.
    /// </summary>
    /// <remarks>
    /// The logout carrier matters because the sign-out screens are reached by an opaque identifier: without
    /// reading it, a session that started in Portuguese would say goodbye in English.
    /// </remarks>
    private static async Task<string?> ResolveUiLocalesAsync(HttpContext httpContext, Realm realm)
    {
        if (httpContext.Request.Query.TryGetValue(Oidc.Authorize.Request.UiLocales, out var inline)
            && !string.IsNullOrWhiteSpace(inline))
        {
            return inline!;
        }

        try
        {
            if (httpContext.Request.Query.TryGetValue(UI.Routes.Params.LogoutId, out var logoutId)
                && !string.IsNullOrWhiteSpace(logoutId))
            {
                var messageStore = httpContext.RequestServices.GetService<IMessageStore>();
                if (messageStore is not null)
                {
                    // Read only: consuming the message here would break the logout flow that owns it.
                    var logout = await messageStore.ReadAsync<LogoutMessage>(logoutId!, httpContext.RequestAborted);
                    if (logout?.Data?.UiLocales is { } logoutLocales)
                        return logoutLocales;
                }
            }

            if (!httpContext.Request.Query.TryGetValue(UI.Routes.Params.ReturnUrl, out var returnUrl)
                || string.IsNullOrWhiteSpace(returnUrl))
            {
                return null;
            }

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

        // RFC 9110: q=0 means the client refuses that language, so it is dropped rather than ranked last.
        foreach (var language in languages
            .Where(value => (value.Quality ?? 1d) > 0d)
            .OrderByDescending(value => value.Quality ?? 1d))
        {
            if (LocaleMatcher.TryMatch(language.Value.Value, supported, out matched))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Culture for a request with no realm: the browser's preference, limited to what the composed UI ships.
    /// </summary>
    private ProviderCultureResult? DetermineCatalogueCulture(HttpContext httpContext)
    {
        if (catalog.NeutralLocale is null)
            return null;

        return TryMatchAcceptLanguage(httpContext, catalog.AvailableLocales, out var accepted)
            ? new ProviderCultureResult(accepted!, accepted!)
            : new ProviderCultureResult(catalog.NeutralLocale, catalog.NeutralLocale);
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
