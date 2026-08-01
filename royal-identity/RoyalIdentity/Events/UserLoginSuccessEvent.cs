// This file contains material derived from IdentityServer4 and/or IdentityModel.
// Original component copyrights remain with Brock Allen, Dominick Baier, and/or Duende Software.
// Licensed under Apache License 2.0; see LICENSES/Apache-2.0.txt and THIRD-PARTY-NOTICES.md.
// Modified by RoyalIdentity contributors for the RoyalIdentity rearchitecture.
//
using RoyalIdentity.Users.Contexts;

namespace RoyalIdentity.Events;

public class UserLoginSuccessEvent : Event
{
    public UserLoginSuccessEvent(string username, string subjectId, AuthorizationContext? context)
        : base(EventCategories.Authentication, "User Login Success Event", EventTypes.Success)
    {
        Username = username;
        SubjectId = subjectId;
        Context = context;
    }

    /// <summary>The login identifier the user typed (for audit). Not necessarily the <c>sub</c>.</summary>
    public string Username { get; }

    /// <summary>The stable subject id (<c>sub</c>) of the authenticated user.</summary>
    public string SubjectId { get; }

    public AuthorizationContext? Context { get; }
}
