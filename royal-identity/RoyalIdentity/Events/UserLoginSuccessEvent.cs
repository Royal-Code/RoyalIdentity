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
