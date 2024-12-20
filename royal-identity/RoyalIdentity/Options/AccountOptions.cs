namespace RoyalIdentity.Options;

public class AccountOptions
{
    public bool AllowLocalLogin { get; set; } = true;

    public bool AllowRememberLogin { get; set; } = true;

    public TimeSpan RememberMeLoginDuration { get; set; } = TimeSpan.FromDays(30);

    [Redesign("Property from the client")]
    public bool ShowLogoutPrompt { get; set; } = true;

    public bool AutomaticRedirectAfterSignOut { get; set; } = true;

    public string InvalidCredentialsErrorMessage { get; set; } = "Invalid username or password";

    public string InactiveUserErrorMessage { get; set; } = "Invalid username or password";

    public string BlockedUserErrorMessage { get; set; } = "Invalid username or password";

    public int UserBlockingAttempts { get; set; } = 3;
}