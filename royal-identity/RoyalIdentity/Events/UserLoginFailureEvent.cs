// This file contains material derived from IdentityServer4 and/or IdentityModel.
// Original component copyrights remain with Brock Allen, Dominick Baier, and/or Duende Software.
// Licensed under Apache License 2.0; see LICENSES/Apache-2.0.txt and THIRD-PARTY-NOTICES.md.
// Modified by RoyalIdentity contributors for the RoyalIdentity rearchitecture.
//
using RoyalIdentity.Users.Contexts;
using RoyalIdentity.Users;

namespace RoyalIdentity.Events;

public class UserLoginFailureEvent : Event
{
    public UserLoginFailureEvent(
        string username,
        string message,
        AuthenticationFailureReason? reason,
        AuthorizationContext? context)
        : base(EventCategories.Error, "User Login Failure Event", EventTypes.Failure, message)
    {
        Username = username;
        Reason = reason;
        Context = context;
    }

    public string Username { get; }

    /// <summary>The internal failure reason for audit. The public message remains generic.</summary>
    public AuthenticationFailureReason? Reason { get; }

    public AuthorizationContext? Context { get; }
}
