// This file contains material derived from IdentityServer4 and/or IdentityModel.
// Original component copyrights remain with Brock Allen, Dominick Baier, and/or Duende Software.
// Licensed under Apache License 2.0; see LICENSES/Apache-2.0.txt and THIRD-PARTY-NOTICES.md.
// Modified by RoyalIdentity contributors for the RoyalIdentity rearchitecture.
//
namespace RoyalIdentity.Events;

/// <summary>
/// Indicates if the event is a success or fail event.
/// </summary>
public enum EventTypes
{
    /// <summary>
    /// Success event
    /// </summary>
    Success = 1,

    /// <summary>
    /// Failure event
    /// </summary>
    Failure = 2,

    /// <summary>
    /// Information event
    /// </summary>
    Information = 3,

    /// <summary>
    /// Error event
    /// </summary>
    Error = 4
}
