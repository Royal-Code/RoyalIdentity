// This file contains material derived from IdentityServer4 and/or IdentityModel.
// Original component copyrights remain with Brock Allen, Dominick Baier, and/or Duende Software.
// Licensed under Apache License 2.0; see LICENSES/Apache-2.0.txt and THIRD-PARTY-NOTICES.md.
// Modified by RoyalIdentity contributors for the RoyalIdentity rearchitecture.
//
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
