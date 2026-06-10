namespace RoyalIdentity.Options;

/// <summary>
/// Visual branding options configurable per realm.
/// </summary>
public class RealmBrandingOptions
{
    public RealmBrandingOptions()
    {
    }

    public RealmBrandingOptions(RealmBrandingOptions other)
    {
        LogoUri = other.LogoUri;
        FaviconUri = other.FaviconUri;
        PrimaryColor = other.PrimaryColor;
    }

    /// <summary>
    /// URI to the realm's logo image. If null, the default server logo is shown.
    /// </summary>
    public string? LogoUri { get; set; }

    /// <summary>
    /// URI to the realm's favicon. If null, the server default favicon is used.
    /// </summary>
    public string? FaviconUri { get; set; }

    /// <summary>
    /// CSS hex color used as the primary color in account pages (e.g. "#3B82F6").
    /// If null, the default theme color is used.
    /// </summary>
    public string? PrimaryColor { get; set; }
}
