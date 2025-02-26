namespace RoyalIdentity.Options;

[Redesign("Merge with LoginOptions (AccountOptions is a better name)")]
public class AccountOptions
{
    public bool AllowLocalLogin { get; set; } = true;

    public bool AllowRememberLogin { get; set; } = true;

    public TimeSpan RememberMeLoginDuration { get; set; } = TimeSpan.FromDays(30);

    public bool AutomaticRedirectAfterSignOut { get; set; } = true;

    [Redesign("Usar Resource")]
    public string InvalidCredentialsErrorMessage { get; set; } = "Invalid username or password";

    [Redesign("Usar Resource")]
    public string InactiveUserErrorMessage { get; set; } = "Invalid username or password";

    [Redesign("Usar Resource")]
    public string BlockedUserErrorMessage { get; set; } = "Invalid username or password";

    public int UserBlockingAttempts { get; set; } = 3;
}