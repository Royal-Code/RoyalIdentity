// This file contains material derived from IdentityServer4 and/or IdentityModel.
// Original component copyrights remain with Brock Allen, Dominick Baier, and/or Duende Software.
// Licensed under Apache License 2.0; see LICENSES/Apache-2.0.txt and THIRD-PARTY-NOTICES.md.
// Modified by RoyalIdentity contributors for the RoyalIdentity rearchitecture.
//
namespace RoyalIdentity.Events;

/// <summary>
/// Categories for events
/// </summary>
public static class EventCategories
{
    /// <summary>
    /// Authentication related events
    /// </summary>
    public const string Authentication = "Authentication";

    /// <summary>
    /// Authorize endpoint related events
    /// </summary>
    public const string Authorize = "Authorize";

    /// <summary>
    /// Token endpoint related events
    /// </summary>
    public const string Token = "Token";

    /// <summary>
    /// Grants related events
    /// </summary>
    public const string Grants = "Grants";

    /// <summary>
    /// Error related events
    /// </summary>
    public const string Error = "Error";

    /// <summary>
    /// Device flow related events
    /// </summary>
    public const string DeviceFlow = "Device";
}
