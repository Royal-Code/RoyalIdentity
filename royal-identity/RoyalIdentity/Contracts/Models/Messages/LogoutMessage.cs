namespace RoyalIdentity.Contracts.Models.Messages;

public class LogoutMessage
{
    public required string SessionId { get; init; }

    public string? PostLogoutRedirectUri { get; init; }

    public string? ClientName { get; init; }

    public bool ShowSignoutPrompt { get; init; }

    public string? State { get; init; }

    public string? UiLocales { get; init; }
}