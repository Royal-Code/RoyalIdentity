namespace RoyalIdentity.Contracts.Models.Messages;

public class LogoutMessage
{
    public required string SessionId { get; set; }

    public string? PostLogoutRedirectUri { get; set; }

    public bool ShowSignoutPrompt { get; set; }

    public string? State { get; set; }

    public string? UiLocales { get; set; }
}