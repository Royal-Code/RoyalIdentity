using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace RoyalIdentity.Localization;

/// <summary>
/// The single place that decides whether a string is a language tag this product accepts, and what its
/// canonical form is.
/// </summary>
/// <remarks>
/// <para>
/// It exists because <see cref="CultureInfo"/> alone is not that decision. Measured on .NET 10,
/// <c>GetCultureInfo(tag, predefinedOnly: true)</c> still accepts two inputs that are not language tags: the
/// empty string resolves to the invariant culture, and <c>en_US</c> resolves to a culture named <c>en_us</c>
/// whose parent is <c>en</c> — so an underscore-separated string would silently negotiate as English.
/// </para>
/// <para>
/// Both realm configuration and request negotiation route through here, so the two cannot drift: a tag the
/// options refuse to store can never be matched at request time either.
/// </para>
/// </remarks>
public static class LanguageTag
{
    /// <summary>
    /// Resolves <paramref name="tag"/> to its canonical <see cref="CultureInfo.Name"/>.
    /// </summary>
    public static bool TryNormalize(string? tag, [NotNullWhen(true)] out string? normalized)
    {
        normalized = null;

        if (!IsWellFormed(tag))
            return false;

        try
        {
            var culture = CultureInfo.GetCultureInfo(tag.Trim(), predefinedOnly: true);

            // The invariant culture answers to the empty name and is not a locale anyone can offer or request.
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
    /// Checks the BCP 47 shape: ASCII alphanumeric subtags of at most eight characters separated by
    /// <c>'-'</c>, the first one alphabetic.
    /// </summary>
    private static bool IsWellFormed([NotNullWhen(true)] string? tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
            return false;

        var subtagLength = 0;
        var isFirstSubtag = true;

        foreach (var character in tag.Trim())
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
