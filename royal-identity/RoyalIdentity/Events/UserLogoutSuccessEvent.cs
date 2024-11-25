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