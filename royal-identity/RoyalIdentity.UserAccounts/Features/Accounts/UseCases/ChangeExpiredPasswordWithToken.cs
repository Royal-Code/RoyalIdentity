using RoyalCode.SmartCommands;
using RoyalCode.SmartProblems;
using RoyalCode.SmartValidations;
using RoyalIdentity.UserAccounts.Features.Accounts.Commons;
using RoyalIdentity.UserAccounts.Features.Accounts.Domain;
using RoyalIdentity.UserAccounts.Options;

namespace RoyalIdentity.UserAccounts.Features.Accounts.UseCases;

/// <summary>
/// Completes the forced/expired password-change flow by consuming a short-lived
/// <see cref="ActionTokenPurpose.ChangeExpiredPassword"/> token and setting a new password. The flow is independent
/// from <see cref="UserAccountsRealmOptions.AllowChangePassword"/> because it satisfies a required action, not a
/// voluntary password change.
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
	/// Executes the forced/expired password change.
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

		return account.SetPassword(
			passwordHasher.Hash(NewPassword),
			now,
			Options.PasswordOptions,
			PasswordChangeReason.Change);
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
