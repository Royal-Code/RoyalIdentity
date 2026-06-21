using RoyalIdentity.UserAccounts.Options;

namespace Tests.UserAccounts;

/// <summary>
/// Shared test fixture for the UserAccounts suite: builds realm options with the password complexity relaxed so
/// seeds and tests can use simple passwords. Centralized here so the domain, use-case, integration and contract
/// tests don't drift apart on the password policy they assume.
/// </summary>
internal static class UserAccountsTestOptions
{
	/// <summary>
	/// Builds realm options with the password complexity rules relaxed for tests.
	/// </summary>
	/// <param name="minimumLength">The minimum password length to enforce.</param>
	/// <param name="allowProvidedSubjectId">Whether callers may provide a deterministic subject id.</param>
	/// <returns>The relaxed realm options.</returns>
	public static UserAccountsRealmOptions Relaxed(int minimumLength = 4, bool allowProvidedSubjectId = false)
	{
		var options = new UserAccountsRealmOptions
		{
			AllowProvidedSubjectId = allowProvidedSubjectId
		};
		options.PasswordOptions.MinimumLength = minimumLength;
		options.PasswordOptions.RequireSpecialCharacters = false;
		options.PasswordOptions.RequireDigit = false;
		options.PasswordOptions.RequireUppercase = false;
		options.PasswordOptions.RequireLowercase = false;
		options.PasswordOptions.MinimumUniqueCharacters = 0;
		options.PasswordOptions.DisallowUsernameInPassword = false;
		return options;
	}
}
