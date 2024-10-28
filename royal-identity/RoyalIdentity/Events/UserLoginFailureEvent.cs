using RoyalIdentity.Users.Contexts;

namespace RoyalIdentity.Events;

public class UserLoginFailureEvent : Event
{
    public UserLoginFailureEvent(string username, string message, AuthorizationContext? context)
        : base(EventCategories.Error, "User Login Failure Event", EventTypes.Failure, message)
    {
        Username = username;
        Context = context;
    }

    public string Username { get; }

    public AuthorizationContext? Context { get; }
}