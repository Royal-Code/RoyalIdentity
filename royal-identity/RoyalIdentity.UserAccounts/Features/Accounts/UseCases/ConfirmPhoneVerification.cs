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
/// Confirms a phone by consuming a verification token (ADR-017 §2.8), gated by the realm phone feature
/// (<see cref="UserAccountsRealmOptions.EnablePhoneNumber"/>). Consumption is idempotent and single-use; the token
/// is bound to the number it was issued for. A missing/expired/consumed token or a target that no longer exists
/// yields the same generic failure.
/// <para>
/// Optimistic-concurrency retry (ADR-017 §2.9) is scoped to the aggregate mutation only, applied manually
/// (<c>IWorkContext.RetryOnConcurrencyAsync</c>) instead of <c>[WithRetryOnConcurrency]</c>: the token consumption
/// above is a single-use, immediately-committed side effect that must never re-run on a retry (Q3).
/// </para>
/// <para>
/// This plan delivers the use case + costura only; the HTTP/UI that drives it belongs to the admin/UI plan (Q12).
/// </para>
/// </summary>
public partial class ConfirmPhoneVerification
{
	/// <summary>
	/// Gets or sets the owning realm.
	/// </summary>
	public string RealmId { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the realm account policies (phone feature toggle).
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
		return Rules.Set<ConfirmPhoneVerification>()
			.NotNull(Options)
			.NotEmpty(RealmId)
			.NotEmpty(Token)
			.HasProblems(out problems);
	}

	/// <summary>
	/// Executes the phone verification confirmation use case. The unit of work is committed manually (not by a
	/// <c>[WithWorkContext]</c>-generated accessor) so the retry loop below can be scoped to just the aggregate
	/// mutation, after the token is already consumed.
	/// </summary>
	[Command, WithValidateModel]
	public async Task<Result> Execute(
		IWorkContext work,
		UserAccountReader reader,
		UserAccountActionTokenService tokens,
		IOptions<RetryOnConcurrencyOptions> retryOptions,
		TimeProvider clock,
		CancellationToken ct)
	{
		if (!Options.EnablePhoneNumber)
		{
			return Problems.NotAllowed(
				"Phone numbers are not enabled for this realm.",
				nameof(Options.EnablePhoneNumber),
				"user_account.phone_not_enabled");
		}

		var now = clock.GetUtcNow();

		var candidate = await tokens.FindConsumableAsync(RealmId, ActionTokenPurpose.PhoneVerification, Token, now, ct);
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

		var targetValue = candidate.TargetValue;
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

				var result = fresh.VerifyPhone(targetValue, now);
				if (!result.IsSuccess)
				{
					return InvalidToken();
				}

				// Return the save outcome itself: a non-concurrency persistence failure (DbUpdateException) must
				// surface as a problem, not be discarded (the token is already consumed at this point).
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
			"The verification token is invalid or has expired.",
			nameof(Token),
			"user_account.action_token_invalid");
	}
}
