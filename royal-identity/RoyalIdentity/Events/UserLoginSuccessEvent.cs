using RoyalIdentity.Users;
using RoyalIdentity.Users.Contexts;

namespace RoyalIdentity.Events
{
    public class UserLoginSuccessEvent : Event
    {
        public UserLoginSuccessEvent(string username, IdentityUser user, AuthorizationContext? context)
            : base(EventCategories.Error, "User Login Failure Event", EventTypes.Failure)
        {
            Username = username;
            User = user;
            Context = context;
        }

        public string Username { get; }

        public IdentityUser User { get; }

        public AuthorizationContext? Context { get; }
    }
}