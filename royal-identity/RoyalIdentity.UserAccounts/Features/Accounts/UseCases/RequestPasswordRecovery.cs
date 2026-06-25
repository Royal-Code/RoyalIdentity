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
/// User-facing "forgot my password" flow (gated by <see cref="UserAccountsRealmOptions.AllowForgotPassword"/>).
/// The public outcome is <em>always the same</em> regardless of whether the account exists or is eligible: a token
/// is issued and delivered only for an active account with a password and a destination address, and a new issuance
/// revokes the account's previous recovery tokens (ADR-017 §2.4). The raw token never leaves the command boundary:
/// it goes straight to the notification gateway and never into the result or the HTTP response (anti-enumeration).
/// <para>
/// The unit of work is committed explicitly (<c>SaveAsync</c>) <em>before</em> the notification is dispatched, so a
/// delivery never references a token that failed to persist (ADR-017 §2.9). Delivery is best-effort: a transport
/// failure leaves the persisted token usable for a retry and does not fail the request.
/// </para>
/// </summary>
public partial class RequestPasswordRecovery
{
	/// <summary>
	/// Gets or sets the owning realm.
	/// </summary>
	public string RealmId { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the realm account policies (recovery toggle, token lifetime/throttle, login resolution).
	/// </summary>
	public UserAccountsRealmOptions Options { get; set; } = default!;

	/// <summary>
	/// Gets or sets the raw login (username or email) the recovery was requested for.
	/// </summary>
	public string Login { get; set; } = string.Empty;

	/// <summary>
	/// Validates the recovery request input.
	/// </summary>
	/// <param name="problems">The collected problems, when invalid.</param>
	/// <returns><c>true</c> when the input is invalid.</returns>
	public bool HasProblems(out Problems? problems)
	{
		return Rules.Set<RequestPasswordRecovery>()
			.NotNull(Options)
			.NotEmpty(RealmId)
			.NotEmpty(Login)
			.HasProblems(out problems);
	}

	/// <summary>
	/// Executes the password recovery request use case. The unit of work is committed here (not by the generated
	/// handler) so the notification can be dispatched after the token is durably persisted.
	/// </summary>
	[Command, WithValidateModel]
	public async Task<Result> Execute(
		IWorkContext work,
		UserAccountReader reader,
		UserAccountActionTokenService tokens,
		INotificationGateway notifications,
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

		var account = await reader.FindByLoginAsync(RealmId, Login, Options, ct);

		// Anti-enumeration: an ineligible request returns the same generic success without issuing a token. Only an
		// active account that holds a password and has somewhere to deliver the token proceeds.
		if (account is null || !account.IsActive || account.IsBlocked || !account.LocalCredential.HasPassword)
		{
			return Result.Ok();
		}

		var destination = account.PrimaryEmail;
		if (destination is null)
		{
			return Result.Ok();
		}

		var now = clock.GetUtcNow();

		var cooldown = Options.SecurityLifecycle.PasswordRecoveryResendCooldownSeconds;
		if (cooldown > 0)
		{
			var threshold = now - TimeSpan.FromSeconds(cooldown);
			if (await tokens.HasActiveTokenSinceAsync(
				RealmId, account.Id, ActionTokenPurpose.PasswordRecovery, threshold, now, ct))
			{
				// A recent token is still valid; do not flood the user. The response is identical to a fresh issuance.
				return Result.Ok();
			}
		}

		var expiresAt = now.AddMinutes(Options.SecurityLifecycle.PasswordRecoveryTokenLifetimeMinutes);
		var rawToken = await tokens.IssueAsync(
			account,
			ActionTokenPurpose.PasswordRecovery,
			destination.NormalizedAddress,
			now,
			expiresAt,
			ct);

		// Persist the token (and the revocation of any previous one) before delivering, so the link is always backed
		// by a stored hash.
		await work.SaveAsync(ct);

		var notification = new PasswordRecoveryNotification(
			RealmId,
			account.SubjectId,
			account.DisplayName,
			destination.Address,
			rawToken,
			expiresAt);

		try
		{
			await notifications.SendPasswordRecoveryAsync(notification, ct);
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			// Best-effort delivery: the token is already persisted, so a transport failure can be retried by the
			// user. Surfacing it as a request error would be misleading (the recovery was registered).
		}

		return Result.Ok();
	}
}
