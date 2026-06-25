using RoyalCode.SmartCommands;
using RoyalCode.SmartProblems;
using RoyalCode.SmartValidations;
using RoyalIdentity.UserAccounts.Features.Accounts.Commons;
using RoyalIdentity.UserAccounts.Features.Accounts.Domain;
using RoyalIdentity.UserAccounts.Options;

namespace RoyalIdentity.UserAccounts.Features.Accounts.UseCases;

/// <summary>
/// Confirms an email by consuming a verification token (ADR-017 §2.8). Consumption is idempotent and single-use; the
/// token is bound to the address it was issued for, so it verifies exactly that value (the account's other or later
/// addresses stay unverified). A missing/expired/consumed token or a target that no longer exists yields the same
/// generic failure (the unguessable token is the secret).
/// <para>
/// This plan delivers the use case + costura only; the HTTP/UI that drives it belongs to the admin/UI plan (Q12).
/// </para>
/// </summary>
public partial class ConfirmEmailVerification
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
	/// Gets or sets the raw verification token delivered to the user.
	/// </summary>
	public string Token { get; set; } = string.Empty;

	/// <summary>
	/// Validates the confirmation input.
	/// </summary>
	/// <param name="problems">The collected problems, when invalid.</param>
	/// <returns><c>true</c> when the input is invalid.</returns>
	public bool HasProblems(out Problems? problems)
	{
		return Rules.Set<ConfirmEmailVerification>()
			.NotNull(Options)
			.NotEmpty(RealmId)
			.NotEmpty(Token)
			.HasProblems(out problems);
	}

	/// <summary>
	/// Executes the email verification confirmation use case.
	/// </summary>
	[Command, WithValidateModel, WithWorkContext]
	public async Task<Result> Execute(
		UserAccountReader reader,
		UserAccountActionTokenService tokens,
		TimeProvider clock,
		CancellationToken ct)
	{
		var now = clock.GetUtcNow();

		var candidate = await tokens.FindConsumableAsync(RealmId, ActionTokenPurpose.EmailVerification, Token, now, ct);
		if (candidate is null || candidate.TargetValue is null)
		{
			return InvalidToken();
		}

		var account = await reader.FindByIdAsync(RealmId, candidate.UserAccountId, ct);
		if (account is null)
		{
			return InvalidToken();
		}

		if (!await tokens.TryConsumeAsync(candidate.TokenId, now, ct))
		{
			return InvalidToken();
		}

		var result = account.VerifyEmail(candidate.TargetValue, now);
		return result.IsSuccess ? result : InvalidToken();
	}

	private static Result InvalidToken()
	{
		return Problems.InvalidParameter(
			"The verification token is invalid or has expired.",
			nameof(Token),
			"user_account.action_token_invalid");
	}
}
