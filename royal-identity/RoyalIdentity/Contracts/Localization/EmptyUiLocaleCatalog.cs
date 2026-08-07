namespace RoyalIdentity.Contracts.Localization;

/// <summary>
/// The catalogue of a host that composes no OP user interface — an API-only composition, for instance. It
/// renders nothing, so localization metadata must stay absent rather than promise locales (DF7/DF14).
/// </summary>
public sealed class EmptyUiLocaleCatalog : IUiLocaleCatalog
{
    /// <inheritdoc />
    public string? NeutralLocale => null;

    /// <inheritdoc />
    public IReadOnlyList<string> AvailableLocales => [];

    /// <inheritdoc />
    public bool Supports(string locale) => false;
}
