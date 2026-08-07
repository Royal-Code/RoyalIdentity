using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace RoyalIdentity.Localization;

/// <summary>
/// Matches a requested language tag against the locales a realm actually offers (plan-localization DF6/DF20).
/// </summary>
/// <remarks>
/// A hint is a preference, never a command: anything unmatched returns <see langword="false"/> so the caller
/// falls through to the next precedence level, and no protocol error is ever produced from a locale.
/// </remarks>
public static class LocaleMatcher
{
    /// <summary>
    /// Resolves <paramref name="requested"/> to one of <paramref name="supported"/>, in three steps: exact
    /// match, then each parent culture, then — only when the language has exactly one offered variant — that
    /// variant. Ambiguity is never guessed.
    /// </summary>
    public static bool TryMatch(
        string? requested,
        IReadOnlyList<string> supported,
        [NotNullWhen(true)] out string? matched)
    {
        matched = null;

        if (supported.Count is 0 || !TryGetCulture(requested, out var culture))
            return false;

        // 1. Exact, then every parent: "es-MX" tries "es-MX" and then "es".
        for (var candidate = culture; candidate.Name.Length is not 0; candidate = candidate.Parent)
        {
            matched = supported.FirstOrDefault(
                locale => string.Equals(locale, candidate.Name, StringComparison.OrdinalIgnoreCase));

            if (matched is not null)
                return true;
        }

        // 2. Sibling variant, but only when it is unambiguous. "es-MX" against {"es-419"} resolves; against
        //    {"es-419","es-ES"} it does not, because picking one of them would be an invention (DF20).
        var language = PrimaryLanguage(culture.Name);
        var siblings = supported
            .Where(locale => string.Equals(PrimaryLanguage(locale), language, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        if (siblings.Length is 1)
        {
            matched = siblings[0];
            return true;
        }

        matched = null;
        return false;
    }

    /// <summary>
    /// Resolves the first tag of a space- or comma-separated preference list that the realm offers, preserving
    /// the caller's order.
    /// </summary>
    public static bool TryMatchPreferenceList(
        string? requestedList,
        IReadOnlyList<string> supported,
        [NotNullWhen(true)] out string? matched)
    {
        matched = null;

        if (string.IsNullOrWhiteSpace(requestedList))
            return false;

        foreach (var requested in requestedList.Split([' ', ',', '\t'], StringSplitOptions.RemoveEmptyEntries))
        {
            if (TryMatch(requested, supported, out matched))
                return true;
        }

        return false;
    }

    private static bool TryGetCulture(string? tag, [NotNullWhen(true)] out CultureInfo? culture)
    {
        culture = null;

        if (string.IsNullOrWhiteSpace(tag))
            return false;

        try
        {
            // predefinedOnly keeps an arbitrary client string from materializing as a custom culture.
            var resolved = CultureInfo.GetCultureInfo(tag.Trim(), predefinedOnly: true);
            if (resolved.Name.Length is 0)
                return false;

            culture = resolved;
            return true;
        }
        catch (CultureNotFoundException)
        {
            return false;
        }
    }

    private static string PrimaryLanguage(string locale)
    {
        var separator = locale.IndexOf('-', StringComparison.Ordinal);
        return separator < 0 ? locale : locale[..separator];
    }
}
