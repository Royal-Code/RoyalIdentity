using RoyalIdentity.Users;
using RoyalIdentity.Users.Contexts;

namespace RoyalIdentity.Events;

public class UserLoginSuccessEvent : Event
{
    public UserLoginSuccessEvent(string username, IdentityUser user, AuthorizationContext? context)
        : base(EventCategories.Authentication, "User Login Success Event", EventTypes.Success)
    {
        Username = username;
        User = user;
        Context = context;
    }

    public string Username { get; }

    public IdentityUser User { get; }

    public AuthorizationContext? Context { get; }
}
