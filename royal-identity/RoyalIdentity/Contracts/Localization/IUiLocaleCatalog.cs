namespace RoyalIdentity.Contracts.Localization;

/// <summary>
/// The locales the composed UI actually ships (plan-localization DF7). Realm options express which locales a
/// realm <i>wants</i> to offer; this contract answers which ones a host can really render, so nothing promises
/// a translation that does not exist.
/// </summary>
/// <remarks>
/// The core owns the contract and an empty default. <c>RoyalIdentity.Razor</c> supplies the RESX-backed
/// implementation, so the dependency only ever points from the UI to the core.
/// </remarks>
public interface IUiLocaleCatalog
{
    /// <summary>
    /// Locale of the catalogue's neutral resources, or <see langword="null"/> when no UI is composed.
    /// </summary>
    string? NeutralLocale { get; }

    /// <summary>
    /// Canonical locales this catalogue can render, neutral one included. Empty when no UI is composed.
    /// </summary>
    IReadOnlyList<string> AvailableLocales { get; }

    /// <summary>
    /// Whether the catalogue can render the given canonical locale.
    /// </summary>
    bool Supports(string locale);
}
