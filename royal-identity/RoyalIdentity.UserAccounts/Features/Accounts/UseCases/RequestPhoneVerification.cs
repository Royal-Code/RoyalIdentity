using RoyalCode.SmartCommands;
using RoyalCode.SmartProblems;
using RoyalCode.SmartValidations;
using RoyalCode.WorkContext;
using RoyalIdentity.UserAccounts.Features.Accounts.Commons;
using RoyalIdentity.UserAccounts.Features.Accounts.Domain;
using RoyalIdentity.UserAccounts.Infrastructure.Gateways;
using RoyalIdentity.UserAccounts.Options;

namespace RoyalIdentity.UserAccounts.Features.Accounts.UseCases;

/// <summary>
/// Issues a phone verification token bound to a specific account phone (ADR-017 §2.8), gated by the realm phone
/// feature (<see cref="UserAccountsRealmOptions.EnablePhoneNumber"/>). Like email verification, the public outcome
/// is the same whether or not a token was issued, and the token's <c>TargetValue</c> binds it to the normalized
/// number so a value replaced later can never be verified with it. The raw token never leaves the command boundary.
/// <para>
/// The unit of work is committed explicitly (<c>SaveAsync</c>) <em>before</em> the notification is dispatched
/// (ADR-017 §2.9). Delivery is best-effort: a transport failure leaves the persisted token usable for a retry.
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
	/// Executes the phone verification request use case. The unit of work is committed here (not by the generated
	/// handler) so the notification can be dispatched after the token is durably persisted.
	/// </summary>
	[Command, WithValidateModel]
	public async Task<Result> Execute(
		IWorkContext work,
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

		await work.SaveAsync(ct);

		var notification = new PhoneVerificationNotification(
			RealmId,
			account.SubjectId,
			account.DisplayName,
			phone.Number,
			rawToken,
			expiresAt);

		try
		{
			await notifications.SendPhoneVerificationAsync(notification, ct);
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			// Best-effort delivery: the token is already persisted, so a transport failure can be retried.
		}

		return Result.Ok();
	}
}
