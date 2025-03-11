namespace RoyalIdentity.Contracts.Models.Messages;

public class LogoutCallbackMessage
{
    public string? SessionId { get; set; }

    public string? PostLogoutRedirectUri { get; set; }

    public string? ClientName { get; set; }

    public string? State { get; set; }

    public string? UiLocales { get; set; }

    public HashSet<string>? FrontChannelLogout { get; set; }

    public bool AutomaticRedirectAfterSignOut { get; set; }

    public required string SignOutIframeUrl { get; set; }

    public bool HasFrontChannel()
    {
        return FrontChannelLogout is not null && FrontChannelLogout.Count is not 0;
    }
}
