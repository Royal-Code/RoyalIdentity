// Ignore Spelling: Username

namespace RoyalIdentity.Options;

/// <summary>
/// Represents the internationalization settings of the realm.
/// </summary>
public class InternationalizationOptions
{
    /// <summary>
    /// Determines if the internationalization is enabled.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Default locale of the realm.
    /// </summary>
    public string? DefaultLocale { get; set; }

    /// <summary>
    /// Supported locales of the realm.
    /// </summary>
    public HashSet<string> SupportedLocales { get; } = [];
}
