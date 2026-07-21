using Microsoft.Extensions.Options;
using RoyalCode.SmartCommands;
using RoyalCode.SmartCommands.WorkContext.Options;
using RoyalCode.SmartProblems;
using RoyalCode.SmartValidations;
using RoyalCode.WorkContext;
using RoyalIdentity.UserAccounts.Features.Accounts.Commons;
using RoyalIdentity.UserAccounts.Features.Accounts.Domain;
using RoyalIdentity.UserAccounts.Options;

namespace RoyalIdentity.UserAccounts.Features.Accounts.UseCases;

/// <summary>
/// Completes the forced/expired password-change flow by consuming a short-lived
/// <see cref="ActionTokenPurpose.ChangeExpiredPassword"/> token and setting a new password. The flow is independent
/// from <see cref="UserAccountsRealmOptions.AllowChangePassword"/> because it satisfies a required action, not a
/// voluntary password change.
/// <para>
/// Optimistic-concurrency retry (ADR-017 §2.9) is scoped to the aggregate mutation only, applied manually
/// (<c>IWorkContext.RetryOnConcurrencyAsync</c>) instead of <c>[WithRetryOnConcurrency]</c>: the token consumption
/// above is a single-use, immediately-committed side effect that must never re-run on a retry (Q3).
/// </para>
/// </summary>
public partial class ChangeExpiredPasswordWithToken
{
	/// <summary>
	/// Gets or sets the owning realm.
	/// </summary>
	public string RealmId { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the realm account policies.
	/// </summary>
	public UserAccountsRealmOptions Options { get; set; } = default!;

	/// <summary>
	/// Gets or sets the raw change-expired-password token.
	/// </summary>
	public string Token { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the new plain password.
	/// </summary>
	public string NewPassword { get; set; } = string.Empty;

	/// <summary>
	/// Validates the forced-change input.
	/// </summary>
	/// <param name="problems">The collected problems, when invalid.</param>
	/// <returns><c>true</c> when the input is invalid.</returns>
	public bool HasProblems(out Problems? problems)
	{
		return Rules.Set<ChangeExpiredPasswordWithToken>()
			.NotNull(Options)
			.NotEmpty(RealmId)
			.NotEmpty(Token)
			.NotEmpty(NewPassword)
			.HasProblems(out problems);
	}

	/// <summary>
	/// Executes the forced/expired password change. The unit of work is committed manually (not by a
	/// <c>[WithWorkContext]</c>-generated accessor) so the retry loop below can be scoped to just the aggregate
	/// mutation, after the token is already consumed.
	/// </summary>
	[Command, WithValidateModel]
	public async Task<Result> Execute(
		IWorkContext work,
		UserAccountReader reader,
		UserAccountActionTokenService tokens,
		IUserAccountPasswordHasher passwordHasher,
		PasswordPolicy passwordPolicy,
		PasswordHistoryPolicy passwordHistoryPolicy,
		IOptions<RetryOnConcurrencyOptions> retryOptions,
		TimeProvider clock,
		CancellationToken ct)
	{
		var now = clock.GetUtcNow();

		var candidate = await tokens.FindConsumableAsync(RealmId, ActionTokenPurpose.ChangeExpiredPassword, Token, now, ct);
		if (candidate is null)
		{
			return InvalidToken();
		}

		var account = await reader.FindByIdAsync(RealmId, candidate.UserAccountId, ct);
		if (account is null || !StillRequiresPasswordChange(account, now))
		{
			return InvalidToken();
		}

		var policyResult = passwordPolicy.Validate(NewPassword, Options.PasswordOptions, account.Username);
		if (policyResult.HasProblems(out var passwordProblems))
		{
			return passwordProblems;
		}

		var historyResult = passwordHistoryPolicy.Validate(NewPassword, account, Options.PasswordOptions, passwordHasher, now);
		if (historyResult.HasProblems(out var historyProblems))
		{
			return historyProblems;
		}

		if (!await tokens.TryConsumeAsync(candidate.TokenId, now, ct))
		{
			return InvalidToken();
		}

		var newPasswordHash = passwordHasher.Hash(NewPassword);
		var accountId = candidate.UserAccountId;

		// Scoped retry (Q3/DF5): only the reload+reapply+save of the aggregate mutation retries. The token was
		// already consumed above, once, and must never re-run here.
		return await work.RetryOnConcurrencyAsync(
			async () =>
			{
				var fresh = await reader.FindByIdAsync(RealmId, accountId, ct);
				if (fresh is null || !StillRequiresPasswordChange(fresh, now))
				{
					// Re-checked against the freshly reloaded state: a concurrent change may already have satisfied
					// the required action since the pre-consumption check above (defense-in-depth under retry).
					return InvalidToken();
				}

				var historyResult = passwordHistoryPolicy.Validate(NewPassword, fresh, Options.PasswordOptions, passwordHasher, now);
				if (historyResult.HasProblems(out var historyProblems))
				{
					return historyProblems;
				}

				var result = fresh.SetPassword(newPasswordHash, now, Options.PasswordOptions, PasswordChangeReason.Change);
				if (result.HasProblems(out _))
				{
					return result;
				}

				// Return the save outcome itself: a non-concurrency persistence failure (DbUpdateException) must
				// surface as a problem, not be discarded in favor of the pre-save domain result (the token is
				// already consumed at this point, so silently returning success here would strand the user).
				return await work.SaveAsync(ct);
			},
			retryOptions.Value,
			onExhausted: () => Problems.InvalidState(
				ConcurrencyRetryExtensions.ConcurrencyConflictDetail,
				typeId: "user_account.concurrency_conflict"),
			ct: ct);
	}

	private bool StillRequiresPasswordChange(UserAccount account, DateTimeOffset now)
	{
		return account.LocalCredential.MustChangePassword ||
			account.LocalCredential.IsPasswordExpired(Options.PasswordOptions, now);
	}

	private static Result InvalidToken()
	{
		return Problems.InvalidParameter(
			"The change-password token is invalid or has expired.",
			nameof(Token),
			"user_account.change_password_token_invalid");
	}
}
