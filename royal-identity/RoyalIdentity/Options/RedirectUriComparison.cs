namespace RoyalIdentity.Options;

/// <summary>
/// Closed comparison modes supported for registered redirect URIs.
/// </summary>
public enum RedirectUriComparison
{
    /// <summary>Compares the complete URI with ordinal, case-sensitive semantics.</summary>
    Ordinal = 0,

    /// <summary>Compares the complete URI with ordinal, case-insensitive semantics.</summary>
    OrdinalIgnoreCase = 1,
}
