namespace RoyalIdentity.Options;

/// <summary>
/// IdP account-flow options.
/// </summary>
public class AccountOptions
{
    public AccountOptions()
    {
    }

    public AccountOptions(AccountOptions other)
    {
        AllowLocalLogin = other.AllowLocalLogin;
        AllowRememberLogin = other.AllowRememberLogin;
        RememberMeLoginDuration = other.RememberMeLoginDuration;
        AllowTwoFactorAuthentication = other.AllowTwoFactorAuthentication;
        AllowSocialLogin = other.AllowSocialLogin;
        AutomaticRedirectAfterSignOut = other.AutomaticRedirectAfterSignOut;
        InvalidCredentialsErrorMessage = other.InvalidCredentialsErrorMessage;
        InactiveUserErrorMessage = other.InactiveUserErrorMessage;
        BlockedUserErrorMessage = other.BlockedUserErrorMessage;
    }

    /// <summary>
    /// Gets or sets whether local username/password login is enabled for the IdP flow.
    /// </summary>
    public bool AllowLocalLogin { get; set; } = true;

    /// <summary>
    /// Gets or sets whether the login flow can persist the realm authentication cookie.
    /// </summary>
    public bool AllowRememberLogin { get; set; } = true;

    /// <summary>
    /// Gets or sets the persistent login cookie duration.
    /// </summary>
    public TimeSpan RememberMeLoginDuration { get; set; } = TimeSpan.FromDays(30);

    /// <summary>
    /// Reserved for future account-security flows. UserAccounts owns the rich account policy.
    /// </summary>
    public bool AllowTwoFactorAuthentication { get; set; } = false;

    /// <summary>
    /// Reserved for future external-login flows.
    /// </summary>
    public bool AllowSocialLogin { get; set; } = false;

    /// <summary>
    /// Gets or sets whether logout can automatically redirect after sign-out when protocol constraints allow it.
    /// </summary>
    public bool AutomaticRedirectAfterSignOut { get; set; } = true;

    /// <summary>
    /// Generic login failure message for invalid credentials.
    /// </summary>
    [Redesign("Usar Resource")]
    public string InvalidCredentialsErrorMessage { get; set; } = "Invalid username or password";

    /// <summary>
    /// Generic login failure message for inactive users.
    /// </summary>
    [Redesign("Usar Resource")]
    public string InactiveUserErrorMessage { get; set; } = "Invalid username or password";

    /// <summary>
    /// Generic login failure message for blocked users.
    /// </summary>
    [Redesign("Usar Resource")]
    public string BlockedUserErrorMessage { get; set; } = "Invalid username or password";
}
