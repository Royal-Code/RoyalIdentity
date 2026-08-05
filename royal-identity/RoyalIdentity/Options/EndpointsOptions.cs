// This file contains material derived from IdentityServer4 and/or IdentityModel.
// Original component copyrights remain with Brock Allen, Dominick Baier, and/or Duende Software.
// Licensed under Apache License 2.0; see LICENSES/Apache-2.0.txt and THIRD-PARTY-NOTICES.md.
// Modified by RoyalIdentity contributors for the RoyalIdentity rearchitecture.
//
namespace RoyalIdentity.Options;

/// <summary>
/// Configures which endpoints are enabled or disabled.
/// </summary>
public class EndpointsOptions
{
    public EndpointsOptions()
    {
    }

    public EndpointsOptions(EndpointsOptions other)
    {
        EnableAuthorizeEndpoint = other.EnableAuthorizeEndpoint;
        EnableJwtRequestUri = other.EnableJwtRequestUri;
        EnableTokenEndpoint = other.EnableTokenEndpoint;
        EnableUserInfoEndpoint = other.EnableUserInfoEndpoint;
        EnableDiscoveryEndpoint = other.EnableDiscoveryEndpoint;
        EnableEndSessionEndpoint = other.EnableEndSessionEndpoint;
        EnableCheckSessionEndpoint = other.EnableCheckSessionEndpoint;
        EnableTokenRevocationEndpoint = other.EnableTokenRevocationEndpoint;
    }

    /// <summary>
    /// Gets or sets a value indicating whether the authorize endpoint is enabled.
    /// </summary>
    /// <value>
    /// <c>true</c> if the authorize endpoint is enabled; otherwise, <c>false</c>.
    /// </value>
    public bool EnableAuthorizeEndpoint { get; set; } = true;

    /// <summary>
    /// Gets or sets if JWT request_uri processing is enabled on the authorize endpoint. 
    /// </summary>
    public bool EnableJwtRequestUri { get; set; } = false;

    /// <summary>
    /// Gets or sets a value indicating whether the token endpoint is enabled.
    /// </summary>
    /// <value>
    /// <c>true</c> if the token endpoint is enabled; otherwise, <c>false</c>.
    /// </value>
    public bool EnableTokenEndpoint { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether the user info endpoint is enabled.
    /// </summary>
    /// <value>
    /// <c>true</c> if the user info endpoint is enabled; otherwise, <c>false</c>.
    /// </value>
    public bool EnableUserInfoEndpoint { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether the discovery document endpoint is enabled.
    /// </summary>
    /// <value>
    /// <c>true</c> if the discovery document endpoint is enabled; otherwise, <c>false</c>.
    /// </value>
    public bool EnableDiscoveryEndpoint { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether the end session endpoint is enabled.
    /// </summary>
    /// <value>
    /// <c>true</c> if the end session endpoint is enabled; otherwise, <c>false</c>.
    /// </value>
    public bool EnableEndSessionEndpoint { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether the check session endpoint is enabled.
    /// </summary>
    /// <value>
    /// <c>true</c> if the check session endpoint is enabled; otherwise, <c>false</c>.
    /// </value>
    public bool EnableCheckSessionEndpoint { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether the token revocation endpoint is enabled.
    /// </summary>
    /// <value>
    /// <c>true</c> if the token revocation endpoint is enabled; otherwise, <c>false</c>.
    /// </value>
    public bool EnableTokenRevocationEndpoint { get; set; } = true;

}
