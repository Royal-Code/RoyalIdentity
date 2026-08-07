using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace RoyalIdentity.Options;

/// <summary>
/// Realm-scoped internationalization policy (plan-localization DF1/DF21/DF22): whether the UI negotiates a
/// culture, which locales the realm offers and in which preference order, and which one it falls back to.
/// </summary>
/// <remarks>
/// These options express the realm's <i>policy</i> only. Which locales the composed UI actually ships is a
/// separate concern owned by the UI locale catalogue (DF7), so nothing here depends on the presentation layer.
/// </remarks>
public class InternationalizationOptions
{
    /// <summary>
    /// Locale of the product's neutral catalogue, and the default locale of a new realm.
    /// </summary>
    public const string NeutralLocale = "en";

    private static readonly string[] DefaultSupportedLocales = [NeutralLocale, "pt-BR", "es-419"];

    /// <summary>
    /// Creates a new instance of <see cref="InternationalizationOptions"/> with the product defaults.
    /// </summary>
    public InternationalizationOptions()
    {
    }

    /// <summary>
    /// Creates a new independent copy of another <see cref="InternationalizationOptions"/> instance.
    /// </summary>
    /// <param name="other">The options to copy.</param>
    public InternationalizationOptions(InternationalizationOptions other)
    {
        ArgumentNullException.ThrowIfNull(other);

        Enabled = other.Enabled;
        DefaultLocale = other.DefaultLocale;
        SupportedLocales.Clear();
        SupportedLocales.AddRange(other.SupportedLocales);
    }

    /// <summary>
    /// Determines whether the realm negotiates a UI culture. Defaults to <see langword="true"/>.
    /// </summary>
    /// <remarks>
    /// Disabling it stops negotiation and discovery metadata, not the need for a coherent policy: the UI still
    /// renders in <see cref="DefaultLocale"/>, which is why the options are validated either way.
    /// </remarks>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Locale used when negotiation produces no supported match. Must be one of <see cref="SupportedLocales"/>.
    /// </summary>
    public string DefaultLocale { get; set; } = NeutralLocale;

    /// <summary>
    /// Locales offered by this realm, in configured preference order.
    /// </summary>
    /// <remarks>
    /// The order is part of the contract and survives copy, payload round-trip and metadata. Only the discovery
    /// response reorders it, by moving <see cref="DefaultLocale"/> to the front.
    /// </remarks>
    public List<string> SupportedLocales { get; } = [.. DefaultSupportedLocales];

    /// <summary>
    /// Canonicalizes <see cref="DefaultLocale"/> and <see cref="SupportedLocales"/> to their
    /// <see cref="CultureInfo.Name"/> form and removes case-insensitive duplicates, keeping the first
    /// occurrence and the configured order.
    /// </summary>
    /// <remarks>
    /// A tag this runtime does not recognize keeps its configured form so <see cref="Validate"/> can name it in
    /// the error; it is still deduplicated, so a repeated invalid tag is reported once.
    /// </remarks>
    public void Normalize()
    {
        if (TryNormalizeLocale(DefaultLocale, out var defaultLocale))
            DefaultLocale = defaultLocale;

        List<string> normalized = [];
        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);

        foreach (var locale in SupportedLocales)
        {
            var candidate = TryNormalizeLocale(locale, out var canonical)
                ? canonical
                : locale ?? string.Empty;

            if (seen.Add(candidate))
                normalized.Add(candidate);
        }

        SupportedLocales.Clear();
        SupportedLocales.AddRange(normalized);
    }

    /// <summary>
    /// Validates internal consistency of the internationalization options.
    /// </summary>
    /// <returns>A list of configuration errors. Empty means valid.</returns>
    public IReadOnlyList<string> Validate()
    {
        List<string> errors = [];

        if (SupportedLocales.Count is 0)
        {
            errors.Add("Internationalization.SupportedLocales must contain at least one locale.");
        }

        foreach (var locale in SupportedLocales)
        {
            if (!TryNormalizeLocale(locale, out _))
            {
                errors.Add(
                    $"Internationalization.SupportedLocales contains '{locale}', which is not a known locale.");
            }
        }

        if (!TryNormalizeLocale(DefaultLocale, out var defaultLocale))
        {
            errors.Add($"Internationalization.DefaultLocale '{DefaultLocale}' is not a known locale.");
        }
        else if (!SupportedLocales.Contains(defaultLocale, StringComparer.OrdinalIgnoreCase))
        {
            errors.Add(
                $"Internationalization.DefaultLocale '{defaultLocale}' must be one of " +
                "Internationalization.SupportedLocales.");
        }

        return errors;
    }

    /// <summary>
    /// Resolves a configured tag to its canonical <see cref="CultureInfo.Name"/>.
    /// </summary>
    internal static bool TryNormalizeLocale(string? tag, [NotNullWhen(true)] out string? normalized)
    {
        normalized = null;

        if (!IsWellFormedLanguageTag(tag))
            return false;

        try
        {
            // predefinedOnly rejects tags this runtime's culture data does not know, such as "zz-ZZ", which the
            // other overload would happily materialize as a custom culture.
            var culture = CultureInfo.GetCultureInfo(tag, predefinedOnly: true);

            // The invariant culture answers to the empty name and is not a locale a realm can offer.
            if (culture.Name.Length is 0)
                return false;

            normalized = culture.Name;
            return true;
        }
        catch (CultureNotFoundException)
        {
            return false;
        }
    }

    /// <summary>
    /// Checks the shape before asking the runtime, because <see cref="CultureInfo"/> also accepts inputs that
    /// are not language tags: <c>en_US</c> resolves and canonicalizes to <c>en_us</c>.
    /// </summary>
    private static bool IsWellFormedLanguageTag([NotNullWhen(true)] string? tag)
    {
        if (string.IsNullOrEmpty(tag))
            return false;

        var subtagLength = 0;
        var isFirstSubtag = true;

        foreach (var character in tag)
        {
            if (character is '-')
            {
                if (subtagLength is 0)
                    return false;

                subtagLength = 0;
                isFirstSubtag = false;
                continue;
            }

            var isValidCharacter = isFirstSubtag
                ? char.IsAsciiLetter(character)
                : char.IsAsciiLetterOrDigit(character);

            if (!isValidCharacter || ++subtagLength > 8)
                return false;
        }

        return subtagLength is not 0;
    }
}
