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
