namespace RoyalIdentity.Options;

/// <summary>
/// Options for password management.
/// </summary>
public class PasswordOptions
{
    /// <summary>
    /// Determines if the forgot password feature is enabled.
    /// Default is true.
    /// </summary>
    public bool AllowForgotPassword { get; set; } = true;

    /// <summary>
    /// Determines if the user can change their password.
    /// Default is true.
    /// </summary>
    public bool AllowChangePassword { get; set; } = true;

    /// <summary>
    /// Determines the maximum number of failed access attempts before a user is locked out.
    /// When zero (0), the feature is disabled.
    /// Default is 3.
    /// </summary>
    public int MaxFailedAccessAttempts { get; set; } = 3;

    /// <summary>
    /// Determines the duration a user is locked out after the maximum number of failed access attempts.
    /// When zero (0), the user will be locked out indefinitely until an administrator unlocks the account.
    /// </summary>
    public int AccountLockoutDurationMinutes { get; set; } = 30;

    /// <summary>
    /// Determines if the password expiration feature is enabled.
    /// Default is true.
    /// </summary>
    public bool EnablePasswordExpiration { get; set; } = true;

    /// <summary>
    /// Determines the number of days before a password expires.
    /// Default is 360.
    /// </summary>
    public int PasswordExpirationDays { get; set; } = 360;

    /// <summary>
    /// Determines if the password history feature is enabled.
    /// Default is true.
    /// </summary>
    public bool EnforcePasswordHistory { get; set; } = true;

    /// <summary>
    /// Determines the number of previous passwords to store.
    /// Default is 3.
    /// </summary>
    public int PasswordHistoryCount { get; set; } = 3;

    /// <summary>
    /// Determines the minimum length of a password.
    /// Default is 6.
    /// </summary>
    public int MinimumLength { get; set; } = 6;

    /// <summary>
    /// Determines the maximum length of a password.
    /// Default is 100.
    /// </summary>
    public int MaximumLength { get; set; } = 100;

    /// <summary>
    /// Determines if a password must contain a special (non-alphanumeric) character.
    /// Default is true.
    /// </summary>
    public bool RequireSpecialCharacters { get; set; } = true;

    /// <summary>
    /// Determines if a password must contain a digit.
    /// </summary>
    public bool RequireDigit { get; set; } = true;

    /// <summary>
    /// Determines if a password must contain a lowercase letter.
    /// </summary>
    public bool RequireLowercase { get; set; } = true;

    /// <summary>
    /// Determines if a password must contain an uppercase letter.
    /// </summary>
    public bool RequireUppercase { get; set; } = true;

    /// <summary>
    /// Determines the minimum number of unique characters in a password.
    /// </summary>
    public int MinimumUniqueCharacters { get; set; } = 6;

    /// <summary>
    /// Determines if the username is not allowed in the password.
    /// Default is true.
    /// </summary>
    public bool DisallowUsernameInPassword { get; set; } = true;

    /// <summary>
    /// Determines if the birthdate is not allowed in the password.
    /// Default is true.
    /// </summary>
    public bool DisallowBirthdateInPassword { get; set; } = true;

    /// <summary>
    /// List of words that are not allowed in the password.
    /// </summary>
    public HashSet<string> DisallowedWordsInPassword { get; set; } = [];
}
