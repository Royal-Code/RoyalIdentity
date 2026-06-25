using RoyalCode.SmartCommands;
using RoyalCode.SmartProblems;
using RoyalCode.SmartValidations;
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
	/// Executes the password reset use case.
	/// </summary>
	[Command, WithValidateModel, WithWorkContext]
	public async Task<Result> Execute(
		UserAccountReader reader,
		UserAccountActionTokenService tokens,
		IUserAccountPasswordHasher passwordHasher,
		PasswordPolicy passwordPolicy,
		PasswordHistoryPolicy passwordHistoryPolicy,
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

		return account.ResetPassword(passwordHasher.Hash(NewPassword), Options.PasswordOptions, now);
	}

	private static Result InvalidToken()
	{
		return Problems.InvalidParameter(
			"The recovery token is invalid or has expired.",
			nameof(Token),
			"user_account.action_token_invalid");
	}
}
