namespace RoyalIdentity.Options;

/// <summary>
/// Options for Content Security Policy
/// </summary>
public class CspOptions
{
    /// <summary>
    /// Creates a new instance of <see cref="CspOptions"/>.
    /// </summary>
    public CspOptions()
    {
    }

    /// <summary>
    /// Creates a new instance of <see cref="CspOptions"/> copying values from another instance.
    /// </summary>
    /// <param name="other">The options to copy.</param>
    public CspOptions(CspOptions other)
    {
        Level = other.Level;
        AddDeprecatedHeader = other.AddDeprecatedHeader;
    }

    /// <summary>
    /// Gets or sets the minimum CSP level.
    /// </summary>
    public CspLevel Level { get; set; } = CspLevel.Two;

    /// <summary>
    /// Gets or sets a value indicating whether the deprected X-Content-Security-Policy header should be added.
    /// </summary>
    public bool AddDeprecatedHeader { get; set; } = true;
}
