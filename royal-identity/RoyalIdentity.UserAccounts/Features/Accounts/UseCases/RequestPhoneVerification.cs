using RoyalCode.SmartCommands;
using RoyalCode.SmartProblems;
using RoyalCode.SmartValidations;
using RoyalIdentity.UserAccounts.Features.Accounts.Commons;
using RoyalIdentity.UserAccounts.Features.Accounts.Domain;
using RoyalIdentity.UserAccounts.Infrastructure.Gateways;
using RoyalIdentity.UserAccounts.Options;

namespace RoyalIdentity.UserAccounts.Features.Accounts.UseCases;

/// <summary>
/// Issues a phone verification token bound to a specific account phone (ADR-017 §2.8), gated by the realm phone
/// feature (<see cref="UserAccountsRealmOptions.EnablePhoneNumber"/>). Like email verification, the public outcome
/// is the same whether or not a token was issued, and the token's <c>TargetValue</c> binds it to the normalized
/// number so a value replaced later can never be verified with it.
/// <para>
/// This plan delivers the use case + costura only; the HTTP/UI that drives it belongs to the admin/UI plan (Q12).
/// </para>
/// </summary>
public partial class RequestPhoneVerification
{
	/// <summary>
	/// Gets or sets the owning realm.
	/// </summary>
	public string RealmId { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the realm account policies (phone feature toggle, token lifetime).
	/// </summary>
	public UserAccountsRealmOptions Options { get; set; } = default!;

	/// <summary>
	/// Gets or sets the subject identifier of the account that owns the phone.
	/// </summary>
	public string SubjectId { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the phone number to verify.
	/// </summary>
	public string PhoneNumber { get; set; } = string.Empty;

	/// <summary>
	/// Validates the verification request input.
	/// </summary>
	/// <param name="problems">The collected problems, when invalid.</param>
	/// <returns><c>true</c> when the input is invalid.</returns>
	public bool HasProblems(out Problems? problems)
	{
		return Rules.Set<RequestPhoneVerification>()
			.NotNull(Options)
			.NotEmpty(RealmId)
			.NotEmpty(SubjectId)
			.NotEmpty(PhoneNumber)
			.HasProblems(out problems);
	}

	/// <summary>
	/// Executes the phone verification request use case.
	/// </summary>
	[Command, WithValidateModel, WithWorkContext]
	public async Task<Result> Execute(
		UserAccountReader reader,
		UserAccountActionTokenService tokens,
		IUserAccountNormalizer normalizer,
		INotificationGateway notifications,
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

		var account = await reader.FindBySubjectIdAsync(RealmId, SubjectId, ct);
		if (account is null || !account.IsActive)
		{
			return Result.Ok();
		}

		var normalizedNumber = normalizer.NormalizePhoneNumber(PhoneNumber);
		var phone = account.Phones.FirstOrDefault(p => p.NormalizedNumber == normalizedNumber);
		if (phone is null || phone.IsVerified)
		{
			return Result.Ok();
		}

		var now = clock.GetUtcNow();
		var expiresAt = now.AddMinutes(Options.SecurityLifecycle.PhoneVerificationTokenLifetimeMinutes);
		var rawToken = await tokens.IssueAsync(
			account,
			ActionTokenPurpose.PhoneVerification,
			normalizedNumber,
			now,
			expiresAt,
			ct);

		await notifications.SendPhoneVerificationAsync(
			new PhoneVerificationNotification(
				RealmId,
				account.SubjectId,
				account.DisplayName,
				phone.Number,
				rawToken,
				expiresAt),
			ct);

		return Result.Ok();
	}
}
