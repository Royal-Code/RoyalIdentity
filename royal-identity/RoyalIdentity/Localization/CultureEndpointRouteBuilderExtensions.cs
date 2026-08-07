using Microsoft.AspNetCore.Antiforgery;
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
    /// A dedicated endpoint keeps this preference action independent from each account page's named form and
    /// model binding.
    /// <para>
    /// The token is validated here, explicitly. Reading <c>Request.Form</c> by hand does <b>not</b> give an
    /// endpoint antiforgery metadata — only model-bound form parameters do — so relying on the host's
    /// middleware would have left this state-changing POST open to cross-site submission.
    /// </para>
    /// </remarks>
    public static IEndpointRouteBuilder MapRealmCultureSelection(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapPost("{realm}/account/culture", async (
            HttpContext httpContext,
            IAntiforgery antiforgery,
            ICulturePreferenceService culturePreference) =>
        {
            if (!httpContext.TryGetCurrentRealm(out var realm))
                return Results.NotFound();

            try
            {
                await antiforgery.ValidateRequestAsync(httpContext);
            }
            catch (AntiforgeryValidationException)
            {
                return Results.BadRequest();
            }

            var form = await httpContext.Request.ReadFormAsync();
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
