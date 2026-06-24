using RoyalCode.SmartProblems;
using RoyalIdentity.UserAccounts.Options;

namespace RoyalIdentity.UserAccounts.Features.Accounts.Domain;

/// <summary>
/// Reusable password reuse policy for account features (ADR-017 §2.2). Rejects a candidate password that matches
/// the current password or a recently used one. Like <see cref="PasswordPolicy"/>, this is a feature-level
/// validation: it runs before the aggregate mutates. Comparison is done by hashing the candidate against each
/// stored hash (salted hashes cannot be compared directly), bounded by the realm comparison cap.
/// </summary>
public class PasswordHistoryPolicy
{
	/// <summary>
	/// Validates that a candidate password is not the current password nor a recently used one.
	/// </summary>
	/// <param name="candidatePassword">The new plain password.</param>
	/// <param name="account">The account whose current credential and history are compared.</param>
	/// <param name="options">The realm password and history policy.</param>
	/// <param name="passwordHasher">The password hasher used to verify the candidate against stored hashes.</param>
	/// <param name="now">The timestamp used to evaluate the reuse age window.</param>
	/// <returns>A result describing whether the candidate is allowed.</returns>
	public Result Validate(
		string candidatePassword,
		UserAccount account,
		PasswordOptions options,
		IUserAccountPasswordHasher passwordHasher,
		DateTimeOffset now)
	{
		if (!options.EnforcePasswordHistory)
		{
			return Result.Ok();
		}

		// Reusing the current password is always a reuse.
		var currentHash = account.LocalCredential.PasswordHash;
		if (!string.IsNullOrWhiteSpace(currentHash) &&
			passwordHasher.Verify(candidatePassword, currentHash!))
		{
			return Reused();
		}

		DateTimeOffset? windowStart = options.PasswordReuseWindowDays > 0
			? now - TimeSpan.FromDays(options.PasswordReuseWindowDays)
			: null;

		var ordered = account.PasswordHistory
			.OrderByDescending(h => h.CreatedAt)
			.ThenByDescending(h => h.Id)
			.ToList();

		var comparisons = 0;
		for (var index = 0; index < ordered.Count; index++)
		{
			if (comparisons >= options.MaxPasswordHistoryComparisons)
			{
				break;
			}

			var entry = ordered[index];
			var withinCount = index < options.PasswordHistoryCount;
			var withinAge = windowStart is not null && entry.CreatedAt >= windowStart;
			if (!withinCount && !withinAge)
			{
				continue;
			}

			comparisons++;
			if (passwordHasher.Verify(candidatePassword, entry.PasswordHash))
			{
				return Reused();
			}
		}

		return Result.Ok();
	}

	private static Result Reused()
	{
		return Problems.InvalidParameter(
			"Password matches a recently used password.",
			"password",
			"user_account.password_reused");
	}
}
