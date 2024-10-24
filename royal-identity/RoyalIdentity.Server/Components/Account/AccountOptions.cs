namespace RoyalIdentity.Server.Components.Account;

[Redesign("Make use of IOptions")]
public static class AccountOptions
{
    public static bool AllowLocalLogin { get; set; } = true;

    public static bool AllowRememberLogin { get; set; } = true;

    public static TimeSpan RememberMeLoginDuration { get; set; } = TimeSpan.FromDays(30);

    public static bool ShowLogoutPrompt { get; set; } = true;

    public static bool AutomaticRedirectAfterSignOut { get; set; } = false;

    public static string InvalidCredentialsErrorMessage { get; set; } = "Invalid username or password";
}