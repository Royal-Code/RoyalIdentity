using System.Globalization;
using Microsoft.Extensions.Localization;
using RoyalIdentity.Contracts.Localization;

namespace RoyalIdentity.Razor.Localization;

/// <summary>
/// The catalogue this UI actually ships (plan-localization DF7). It reports what the embedded RESX catalogues
/// can render, so discovery metadata and culture negotiation never promise a translation that is not there.
/// </summary>
/// <remarks>
/// Availability is asserted, not assumed: a culture only counts when its catalogue resolves a probe key to a
/// value that is not the key itself, which is what <see cref="IStringLocalizer"/> returns on a miss.
/// </remarks>
public sealed class ResxUiLocaleCatalog : IUiLocaleCatalog
{
    /// <summary>
    /// A key that must exist in every culture of <see cref="AccountResources"/>. Probing a real key is what
    /// distinguishes a shipped catalogue from a satellite assembly that was never built.
    /// </summary>
    internal const string ProbeKey = "Login_Title";

    private readonly IReadOnlyList<string> availableLocales;

    public ResxUiLocaleCatalog(IStringLocalizerFactory localizerFactory)
    {
        ArgumentNullException.ThrowIfNull(localizerFactory);

        var localizer = localizerFactory.Create(typeof(AccountResources));
        availableLocales = ShippedLocales
            .Where(locale => Resolves(localizer, locale))
            .ToArray();
    }

    /// <summary>
    /// Locales this product ships a catalogue for. The neutral one comes first.
    /// </summary>
    internal static IReadOnlyList<string> ShippedLocales { get; } = ["en", "pt-BR", "es-419"];

    /// <inheritdoc />
    public string? NeutralLocale => availableLocales.Count is 0 ? null : availableLocales[0];

    /// <inheritdoc />
    public IReadOnlyList<string> AvailableLocales => availableLocales;

    /// <inheritdoc />
    public bool Supports(string locale)
        => availableLocales.Contains(locale, StringComparer.OrdinalIgnoreCase);

    private static bool Resolves(IStringLocalizer localizer, string locale)
    {
        var previous = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(locale);
            return !localizer[ProbeKey].ResourceNotFound;
        }
        catch (CultureNotFoundException)
        {
            return false;
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }
}
