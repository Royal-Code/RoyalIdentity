// This file contains material derived from IdentityServer4 and/or IdentityModel.
// Original component copyrights remain with Brock Allen, Dominick Baier, and/or Duende Software.
// Licensed under Apache License 2.0; see LICENSES/Apache-2.0.txt and THIRD-PARTY-NOTICES.md.
// Modified by RoyalIdentity contributors for the RoyalIdentity rearchitecture.
//
namespace RoyalIdentity.Events;

public class UserLogoutSuccessEvent : Event
{
    public UserLogoutSuccessEvent(string username, string? sessionId) 
        : base(EventCategories.Authentication, "User Logout Success Event", EventTypes.Success)
    {
        Username = username;
        SessionId = sessionId;
    }

    public string Username { get; }

    public string? SessionId { get; }
}
