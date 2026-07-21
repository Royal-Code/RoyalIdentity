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
/// Completes a password recovery by consuming a recovery token and setting a new password (gated by
/// <see cref="UserAccountsRealmOptions.AllowForgotPassword"/>). The new password is validated against the realm
/// complexity and reuse-history policies (Fase 3) <em>before</em> the token is consumed, so a rejected password
/// leaves the token usable for a retry. Consumption is idempotent (single-use, ADR-017 §2.4); a missing, expired,
/// revoked or already-consumed token yields the same generic failure — the unguessable token is the secret, so this
/// does not reveal account state. The reset moves the session-invalidation marker; the active revocation of existing
/// sessions/refresh tokens is executed at the edge (Fase 8).
/// <para>
/// Optimistic-concurrency retry (ADR-017 §2.9) is scoped to the aggregate mutation only, applied manually
/// (<c>IWorkContext.RetryOnConcurrencyAsync</c>) instead of <c>[WithRetryOnConcurrency]</c>: the token consumption
/// above is a single-use, immediately-committed side effect that must never re-run on a retry (Q3).
/// </para>
/// <para>
/// This plan delivers the use case + costura only; the HTTP/UI that drives it belongs to the admin/UI plan (Q12).
/// </para>
/// </summary>
public partial class ResetPasswordWithToken
{
	/// <summary>
	/// Gets or sets the owning realm.
	/// </summary>
	public string RealmId { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the realm account policies (recovery toggle, complexity and reuse history).
	/// </summary>
	public UserAccountsRealmOptions Options { get; set; } = default!;

	/// <summary>
	/// Gets or sets the raw recovery token delivered to the user.
	/// </summary>
	public string Token { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the new plain password.
	/// </summary>
	public string NewPassword { get; set; } = string.Empty;

	/// <summary>
	/// Validates the reset input.
	/// </summary>
	/// <param name="problems">The collected problems, when invalid.</param>
	/// <returns><c>true</c> when the input is invalid.</returns>
	public bool HasProblems(out Problems? problems)
	{
		return Rules.Set<ResetPasswordWithToken>()
			.NotNull(Options)
			.NotEmpty(RealmId)
			.NotEmpty(Token)
			.NotEmpty(NewPassword)
			.HasProblems(out problems);
	}

	/// <summary>
	/// Executes the password reset use case. The unit of work is committed manually (not by a
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
		if (!Options.AllowForgotPassword)
		{
			return Problems.NotAllowed(
				"Password recovery is not allowed for this realm.",
				nameof(Options.AllowForgotPassword),
				"user_account.forgot_password_not_allowed");
		}

		var now = clock.GetUtcNow();

		var candidate = await tokens.FindConsumableAsync(RealmId, ActionTokenPurpose.PasswordRecovery, Token, now, ct);
		if (candidate is null)
		{
			return InvalidToken();
		}

		var account = await reader.FindByIdAsync(RealmId, candidate.UserAccountId, ct);
		if (account is null)
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

		// Consume last: validation already passed, so the single-use token is only spent on a successful reset.
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
				if (fresh is null)
				{
					return InvalidToken();
				}

				// Re-validated against the freshly reloaded state: a concurrent change could have altered the
				// account's password history since the pre-consumption check above (defense-in-depth under retry).
				var historyResult = passwordHistoryPolicy.Validate(NewPassword, fresh, Options.PasswordOptions, passwordHasher, now);
				if (historyResult.HasProblems(out var historyProblems))
				{
					return historyProblems;
				}

				var result = fresh.ResetPassword(newPasswordHash, Options.PasswordOptions, now);
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

	private static Result InvalidToken()
	{
		return Problems.InvalidParameter(
			"The recovery token is invalid or has expired.",
			nameof(Token),
			"user_account.action_token_invalid");
	}
}
