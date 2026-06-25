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
/// Issues an email verification token bound to a specific account email (ADR-017 §2.8). The public outcome is the
/// same whether or not a token was issued: a token is only created and delivered for an existing, non-fictitious,
/// still-unverified address of an active account. The token's <c>TargetValue</c> is the normalized address, so a
/// value replaced later can never be verified with it. The raw token never leaves the command boundary.
/// <para>
/// The unit of work is committed explicitly (<c>SaveAsync</c>) <em>before</em> the notification is dispatched
/// (ADR-017 §2.9). Delivery is best-effort: a transport failure leaves the persisted token usable for a retry.
/// </para>
/// </summary>
public partial class RequestEmailVerification
{
	/// <summary>
	/// Gets or sets the owning realm.
	/// </summary>
	public string RealmId { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the realm account policies (token lifetime).
	/// </summary>
	public UserAccountsRealmOptions Options { get; set; } = default!;

	/// <summary>
	/// Gets or sets the subject identifier of the account that owns the email.
	/// </summary>
	public string SubjectId { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the email address to verify.
	/// </summary>
	public string Email { get; set; } = string.Empty;

	/// <summary>
	/// Validates the verification request input.
	/// </summary>
	/// <param name="problems">The collected problems, when invalid.</param>
	/// <returns><c>true</c> when the input is invalid.</returns>
	public bool HasProblems(out Problems? problems)
	{
		return Rules.Set<RequestEmailVerification>()
			.NotNull(Options)
			.NotEmpty(RealmId)
			.NotEmpty(SubjectId)
			.NotEmpty(Email)
			.HasProblems(out problems);
	}

	/// <summary>
	/// Executes the email verification request use case. The unit of work is committed here (not by the generated
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
		var account = await reader.FindBySubjectIdAsync(RealmId, SubjectId, ct);
		if (account is null || !account.IsActive)
		{
			return Result.Ok();
		}

		var normalizedAddress = normalizer.NormalizeEmail(Email);
		var email = account.Emails.FirstOrDefault(e => e.NormalizedAddress == normalizedAddress);

		// Nothing to verify: missing, fictitious or already verified addresses produce the same generic success.
		if (email is null || email.IsFictitious || email.IsVerified)
		{
			return Result.Ok();
		}

		var now = clock.GetUtcNow();
		var expiresAt = now.AddMinutes(Options.SecurityLifecycle.EmailVerificationTokenLifetimeMinutes);
		var rawToken = await tokens.IssueAsync(
			account,
			ActionTokenPurpose.EmailVerification,
			normalizedAddress,
			now,
			expiresAt,
			ct);

		await work.SaveAsync(ct);

		var notification = new EmailVerificationNotification(
			RealmId,
			account.SubjectId,
			account.DisplayName,
			email.Address,
			rawToken,
			expiresAt);

		try
		{
			await notifications.SendEmailVerificationAsync(notification, ct);
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			// Best-effort delivery: the token is already persisted, so a transport failure can be retried.
		}

		return Result.Ok();
	}
}
