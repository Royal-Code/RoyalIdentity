using RoyalCode.SmartProblems;
using RoyalIdentity.UserAccounts.Options;

namespace RoyalIdentity.UserAccounts.Features.Accounts.Domain;

/// <summary>
/// Reusable password complexity policy for account features.
/// </summary>
public class PasswordPolicy
{
	/// <summary>
	/// Validates a plain password against the realm password options.
	/// </summary>
	/// <param name="password">The plain password.</param>
	/// <param name="options">The password options.</param>
	/// <param name="username">Optional username used by username-in-password checks.</param>
	/// <returns>A result describing whether the password satisfies the policy.</returns>
	public Result Validate(string password, PasswordOptions options, string? username = null)
	{
		if (string.IsNullOrEmpty(password))
		{
			return Problems.InvalidParameter("Password is required.", nameof(password), "user_account.password_required");
		}

		if (password.Length < options.MinimumLength)
		{
			return Problems.InvalidParameter("Password is shorter than the minimum length.", nameof(password), "user_account.password_too_short");
		}

		if (password.Length > options.MaximumLength)
		{
			return Problems.InvalidParameter("Password is longer than the maximum length.", nameof(password), "user_account.password_too_long");
		}

		if (options.RequireDigit && !password.Any(char.IsDigit))
		{
			return Problems.InvalidParameter("Password must contain a digit.", nameof(password), "user_account.password_requires_digit");
		}

		if (options.RequireLowercase && !password.Any(char.IsLower))
		{
			return Problems.InvalidParameter("Password must contain a lowercase letter.", nameof(password), "user_account.password_requires_lowercase");
		}

		if (options.RequireUppercase && !password.Any(char.IsUpper))
		{
			return Problems.InvalidParameter("Password must contain an uppercase letter.", nameof(password), "user_account.password_requires_uppercase");
		}

		if (options.RequireSpecialCharacters && password.All(char.IsLetterOrDigit))
		{
			return Problems.InvalidParameter("Password must contain a special character.", nameof(password), "user_account.password_requires_special");
		}

		if (options.MinimumUniqueCharacters > 0 &&
			password.Distinct().Count() < options.MinimumUniqueCharacters)
		{
			return Problems.InvalidParameter("Password does not contain enough unique characters.", nameof(password), "user_account.password_unique_chars");
		}

		if (options.DisallowUsernameInPassword &&
			!string.IsNullOrWhiteSpace(username) &&
			password.Contains(username, StringComparison.OrdinalIgnoreCase))
		{
			return Problems.InvalidParameter("Password cannot contain the username.", nameof(password), "user_account.password_contains_username");
		}

		foreach (var disallowedWord in options.DisallowedWordsInPassword)
		{
			if (!string.IsNullOrWhiteSpace(disallowedWord) &&
				password.Contains(disallowedWord, StringComparison.OrdinalIgnoreCase))
			{
				return Problems.InvalidParameter("Password contains a disallowed word.", nameof(password), "user_account.password_disallowed_word");
			}
		}

		return Result.Ok();
	}
}
