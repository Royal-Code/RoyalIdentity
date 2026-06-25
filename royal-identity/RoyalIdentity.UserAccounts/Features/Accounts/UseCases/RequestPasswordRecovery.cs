using RoyalCode.SmartCommands;
using RoyalCode.SmartProblems;
using RoyalCode.SmartValidations;
using RoyalIdentity.UserAccounts.Features.Accounts.Commons;
using RoyalIdentity.UserAccounts.Features.Accounts.Domain;
using RoyalIdentity.UserAccounts.Infrastructure.Gateways;
using RoyalIdentity.UserAccounts.Options;

namespace RoyalIdentity.UserAccounts.Features.Accounts.UseCases;

/// <summary>
/// User-facing "forgot my password" flow (gated by <see cref="UserAccountsRealmOptions.AllowForgotPassword"/>).
/// The public outcome is <em>always the same</em> regardless of whether the account exists or is eligible: a token
/// is issued only for an active account with a password and a destination address, and a new issuance revokes the
/// account's previous recovery tokens (ADR-017 §2.4). Delivery is returned to the trusted edge as a payload so it
/// can happen after the generated handler completes the unit of work. The response never reveals account existence
/// or state (anti-enumeration). IP/identifier-scoped rate limiting is an edge concern; this module applies the
/// per-realm resend throttle.
/// <para>
/// This plan delivers the use case + costura only; the HTTP/UI that drives it belongs to the admin/UI plan (Q12).
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
	/// Executes the password recovery request use case.
	/// </summary>
	[Command, WithValidateModel, WithWorkContext]
	public async Task<Result<PasswordRecoveryRequestResult>> Execute(
		UserAccountReader reader,
		UserAccountActionTokenService tokens,
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
			return PasswordRecoveryRequestResult.NoDelivery;
		}

		var destination = account.PrimaryEmail;
		if (destination is null)
		{
			return PasswordRecoveryRequestResult.NoDelivery;
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
				return PasswordRecoveryRequestResult.NoDelivery;
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

		return PasswordRecoveryRequestResult.Deliver(
			new PasswordRecoveryNotification(
				RealmId,
				account.SubjectId,
				account.DisplayName,
				destination.Address,
				rawToken,
				expiresAt));
	}
}
