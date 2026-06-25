using RoyalCode.SmartCommands;
using RoyalCode.SmartProblems;
using RoyalCode.SmartValidations;
using RoyalIdentity.UserAccounts.Features.Accounts.Commons;
using RoyalIdentity.UserAccounts.Features.Accounts.Domain;
using RoyalIdentity.UserAccounts.Options;

namespace RoyalIdentity.UserAccounts.Features.Accounts.UseCases;

/// <summary>
/// Issues the transient action token for the forced/expired password-change flow. The command re-validates the
/// current credential and only emits a token when authentication is otherwise valid but gated by
/// <see cref="LocalAuthenticationResult.RequiredAction"/>.
/// </summary>
public partial class IssueChangeExpiredPasswordToken
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
	/// Gets or sets the raw login.
	/// </summary>
	public string Login { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the current plain password.
	/// </summary>
	public string Password { get; set; } = string.Empty;

	/// <summary>
	/// Validates the challenge issuance input.
	/// </summary>
	/// <param name="problems">The collected problems, when invalid.</param>
	/// <returns><c>true</c> when the input is invalid.</returns>
	public bool HasProblems(out Problems? problems)
	{
		return Rules.Set<IssueChangeExpiredPasswordToken>()
			.NotNull(Options)
			.NotEmpty(RealmId)
			.NotEmpty(Login)
			.NotEmpty(Password)
			.HasProblems(out problems);
	}

	/// <summary>
	/// Executes the forced/expired password-change challenge issuance.
	/// </summary>
	[Command, WithValidateModel, WithWorkContext]
	public async Task<Result<ChangeExpiredPasswordToken>> Execute(
		UserAccountReader reader,
		UserAccountActionTokenService tokens,
		IUserAccountPasswordHasher passwordHasher,
		TimeProvider clock,
		CancellationToken ct)
	{
		var account = await reader.FindByLoginAsync(RealmId, Login, Options, ct);
		if (account is null)
		{
			return InvalidChallenge();
		}

		var now = clock.GetUtcNow();
		var authentication = account.AuthenticateLocal(Password, Options.PasswordOptions, passwordHasher, now);
		if (authentication.RequiredAction is null)
		{
			return InvalidChallenge();
		}

		var expiresAt = now.AddMinutes(Options.SecurityLifecycle.ChangeExpiredPasswordTokenLifetimeMinutes);

		// No TargetValue binding: the token's validity at consumption is enforced by single-use + TTL +
		// revoke-on-reissue and by re-checking that the account still requires a password change. Binding to the
		// SecurityStamp would over-restrict (any unrelated sensitive change — e.g. an admin email/phone edit — moves
		// the stamp and would needlessly invalidate a still-required challenge).
		var rawToken = await tokens.IssueAsync(
			account,
			ActionTokenPurpose.ChangeExpiredPassword,
			targetValue: null,
			now,
			expiresAt,
			ct);

		return new ChangeExpiredPasswordToken(
			account.SubjectId,
			account.DisplayName,
			rawToken,
			expiresAt,
			authentication.RequiredAction.Value);
	}

	private static Result<ChangeExpiredPasswordToken> InvalidChallenge()
	{
		return Problems.InvalidParameter(
			"The change-password challenge cannot be issued.",
			nameof(Login),
			"user_account.change_password_challenge_invalid");
	}
}
