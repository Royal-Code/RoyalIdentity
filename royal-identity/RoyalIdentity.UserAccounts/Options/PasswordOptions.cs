namespace RoyalIdentity.UserAccounts.Options;

/// <summary>
/// Password and lockout policies for user accounts.
/// </summary>
public class PasswordOptions
{
	/// <summary>
	/// Creates a new instance with default password policy values.
	/// </summary>
	public PasswordOptions()
	{
	}

	/// <summary>
	/// Creates an independent copy of another password policy.
	/// </summary>
	public PasswordOptions(PasswordOptions other)
	{
		MaxFailedAccessAttempts = other.MaxFailedAccessAttempts;
		AccountLockoutDurationMinutes = other.AccountLockoutDurationMinutes;
		EnablePasswordExpiration = other.EnablePasswordExpiration;
		PasswordExpirationDays = other.PasswordExpirationDays;
		EnforcePasswordHistory = other.EnforcePasswordHistory;
		PasswordHistoryCount = other.PasswordHistoryCount;
		PasswordReuseWindowDays = other.PasswordReuseWindowDays;
		MaxPasswordHistoryComparisons = other.MaxPasswordHistoryComparisons;
		MinimumLength = other.MinimumLength;
		MaximumLength = other.MaximumLength;
		RequireSpecialCharacters = other.RequireSpecialCharacters;
		RequireDigit = other.RequireDigit;
		RequireLowercase = other.RequireLowercase;
		RequireUppercase = other.RequireUppercase;
		MinimumUniqueCharacters = other.MinimumUniqueCharacters;
		DisallowUsernameInPassword = other.DisallowUsernameInPassword;
		DisallowBirthdateInPassword = other.DisallowBirthdateInPassword;
		DisallowedWordsInPassword = [.. other.DisallowedWordsInPassword];
	}

	/// <summary>
	/// Gets or sets the maximum number of failed attempts before lockout. Zero disables failed-attempt lockout.
	/// </summary>
	public int MaxFailedAccessAttempts { get; set; } = 3;

	/// <summary>
	/// Gets or sets the lockout duration in minutes. Zero means locked until an administrator unlocks the account.
	/// </summary>
	public int AccountLockoutDurationMinutes { get; set; } = 30;

	/// <summary>
	/// Gets or sets whether password expiration is tracked.
	/// </summary>
	public bool EnablePasswordExpiration { get; set; } = true;

	/// <summary>
	/// Gets or sets the number of days before a password expires.
	/// </summary>
	public int PasswordExpirationDays { get; set; } = 360;

	/// <summary>
	/// Gets or sets whether password history is tracked.
	/// </summary>
	public bool EnforcePasswordHistory { get; set; } = true;

	/// <summary>
	/// Gets or sets the number of previous password hashes to retain.
	/// </summary>
	public int PasswordHistoryCount { get; set; } = 3;

	/// <summary>
	/// Gets or sets the reuse window, in days, for password history checks. Zero disables the age criterion
	/// (only <see cref="PasswordHistoryCount"/> applies). When greater than zero, a candidate password is also
	/// rejected if it matches any hash changed within the window, even beyond the retained count.
	/// </summary>
	public int PasswordReuseWindowDays { get; set; } = 0;

	/// <summary>
	/// Gets or sets the safety cap on how many historical hashes are compared/retained per password change.
	/// Bounds the cost of the quantity-union-age set for accounts that change passwords very often.
	/// </summary>
	public int MaxPasswordHistoryComparisons { get; set; } = 24;

	/// <summary>
	/// Gets or sets the minimum password length.
	/// </summary>
	public int MinimumLength { get; set; } = 6;

	/// <summary>
	/// Gets or sets the maximum password length.
	/// </summary>
	public int MaximumLength { get; set; } = 100;

	/// <summary>
	/// Gets or sets whether a password must contain a special, non-alphanumeric character.
	/// </summary>
	public bool RequireSpecialCharacters { get; set; } = true;

	/// <summary>
	/// Gets or sets whether a password must contain a digit.
	/// </summary>
	public bool RequireDigit { get; set; } = true;

	/// <summary>
	/// Gets or sets whether a password must contain a lowercase letter.
	/// </summary>
	public bool RequireLowercase { get; set; } = true;

	/// <summary>
	/// Gets or sets whether a password must contain an uppercase letter.
	/// </summary>
	public bool RequireUppercase { get; set; } = true;

	/// <summary>
	/// Gets or sets the minimum number of unique characters in a password.
	/// </summary>
	public int MinimumUniqueCharacters { get; set; } = 6;

	/// <summary>
	/// Gets or sets whether the username is disallowed in the password.
	/// </summary>
	public bool DisallowUsernameInPassword { get; set; } = true;

	/// <summary>
	/// Gets or sets whether the birthdate is disallowed in the password.
	/// </summary>
	public bool DisallowBirthdateInPassword { get; set; } = true;

	/// <summary>
	/// Gets or sets words that are disallowed in passwords.
	/// </summary>
	public HashSet<string> DisallowedWordsInPassword { get; set; } = [];
}
