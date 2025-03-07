namespace RoyalIdentity.Options;

/// <summary>
/// Options for account management
/// </summary>
public class AccountOptions
{
    public bool AllowLocalLogin { get; set; } = true;

    public bool AllowRememberLogin { get; set; } = true;

    public TimeSpan RememberMeLoginDuration { get; set; } = TimeSpan.FromDays(30);

    public bool AllowRegistration { get; set; } = false;

    public bool AllowForgotPassword { get; set; } = true;

    public bool AllowChangePassword { get; set; } = true;

    public bool AllowUpdateProfile { get; set; } = false;

    public bool AllowChangeEmail { get; set; } = true;

    public bool AllowChangeUsername { get; set; } = false;

    public bool AllowChangePhoneNumber { get; set; } = true;

    public bool AllowDeleteAccount { get; set; } = false;

    public bool AllowTwoFactorAuthentication { get; set; } = false;

    public bool AllowSocialLogin { get; set; } = false;

    public bool EmailAsUsername { get; set; } = false;

    public bool LoginWithEmail { get; set; } = false;

    public bool AllowDuplicateEmail { get; set; } = false;

    public bool VerifyEmail { get; set; } = false;

    public bool AutomaticRedirectAfterSignOut { get; set; } = true;

    public PasswordOptions PasswordOptions { get; } = new();

    [Redesign("Usar Resource")]
    public string InvalidCredentialsErrorMessage { get; set; } = "Invalid username or password";

    [Redesign("Usar Resource")]
    public string InactiveUserErrorMessage { get; set; } = "Invalid username or password";

    [Redesign("Usar Resource")]
    public string BlockedUserErrorMessage { get; set; } = "Invalid username or password";

    public int UserBlockingAttempts { get; set; } = 3;
}