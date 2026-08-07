using System.Globalization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.DependencyInjection;
using RoyalIdentity.Contracts.Localization;
using RoyalIdentity.Localization;

namespace RoyalIdentity.Extensions;

/// <summary>Installs the realm-scoped request localization used by the OP user interface.</summary>
public static class RequestLocalizationApplicationBuilderExtensions
{
    /// <summary>
    /// Adds request localization driven exclusively by <see cref="RealmRequestCultureProvider"/>.
    /// </summary>
    /// <remarks>
    /// The framework's default providers are replaced rather than appended: query string, a foreign cookie and
    /// a bare <c>Accept-Language</c> would each be able to select a culture the current realm never offered,
    /// which is precisely what the realm-scoped precedence exists to prevent.
    /// </remarks>
    public static IApplicationBuilder UseRealmRequestLocalization(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var catalog = app.ApplicationServices.GetRequiredService<IUiLocaleCatalog>();
        var supported = catalog.AvailableLocales.Count is 0
            ? [CultureInfo.InvariantCulture]
            : catalog.AvailableLocales.Select(CultureInfo.GetCultureInfo).ToList();

        var options = new RequestLocalizationOptions
        {
            ApplyCurrentCultureToResponseHeaders = true,
            SupportedCultures = supported,
            SupportedUICultures = supported,
        };

        if (catalog.NeutralLocale is { } neutral)
            options.SetDefaultCulture(neutral);

        options.RequestCultureProviders.Clear();
        options.RequestCultureProviders.Add(new RealmRequestCultureProvider(catalog));

        return app.UseRequestLocalization(options);
    }
}
