using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using RoyalIdentity.Extensions;

namespace RoyalIdentity.Localization;

/// <summary>Maps the realm-scoped endpoint that stores an explicit language choice.</summary>
public static class CultureEndpointRouteBuilderExtensions
{
    /// <summary>
    /// Maps <c>POST {realm}/account/culture</c>.
    /// </summary>
    /// <remarks>
    /// A dedicated endpoint rather than a Blazor SSR named form: a second named form on a page that already
    /// has one diverts the <c>_handler</c> dispatch and makes the page's own POST fail antiforgery. Antiforgery
    /// still applies here — the host installs the middleware, and this route does not opt out of it.
    /// </remarks>
    public static IEndpointRouteBuilder MapRealmCultureSelection(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapPost("{realm}/account/culture", (
            HttpContext httpContext,
            ICulturePreferenceService culturePreference) =>
        {
            if (!httpContext.TryGetCurrentRealm(out var realm))
                return Results.NotFound();

            var form = httpContext.Request.Form;
            culturePreference.Apply(form["locale"]);

            // The return address is only ever a path inside this realm. Accepting the posted value as-is
            // would turn a language selector into an open redirect.
            var requested = form["returnUrl"].ToString();
            var target = BuildRealmLocalReturnUrl(realm.Path, requested);

            return Results.LocalRedirect(target);
        });

        return endpoints;
    }

    private static string BuildRealmLocalReturnUrl(string realmPath, string? requested)
    {
        var fallback = $"/{realmPath}/account/login";

        if (string.IsNullOrWhiteSpace(requested))
            return fallback;

        // Reject anything that could leave the site: absolute URIs, scheme-relative "//host" and backslash
        // variants browsers normalize into one.
        var candidate = requested.StartsWith('/') ? requested : $"/{requested}";
        if (candidate.StartsWith("//", StringComparison.Ordinal)
            || candidate.StartsWith("/\\", StringComparison.Ordinal)
            || Uri.IsWellFormedUriString(requested, UriKind.Absolute))
        {
            return fallback;
        }

        // ...and anything outside this realm's own path.
        var realmPrefix = $"/{realmPath}/";
        return candidate.StartsWith(realmPrefix, StringComparison.Ordinal) ? candidate : fallback;
    }
}
