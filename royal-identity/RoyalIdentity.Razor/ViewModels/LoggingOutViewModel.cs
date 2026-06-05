namespace RoyalIdentity.Razor.ViewModels;

public class LoggingOutViewModel
{
    public string? PostLogoutRedirectUri { get; set; }
    public string? ClientName { get; set; }
    public bool AutomaticRedirectAfterSignOut { get; set; }
    public string? SignOutIframeUrl { get; set; }

    public bool HasFrontChannel => SignOutIframeUrl is not null;
}
