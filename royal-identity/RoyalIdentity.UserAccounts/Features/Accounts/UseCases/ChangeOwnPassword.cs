using RoyalCode.SmartCommands;
using RoyalCode.SmartProblems;
using RoyalCode.SmartValidations;
using RoyalIdentity.UserAccounts.Features.Accounts.Commons;
using RoyalIdentity.UserAccounts.Features.Accounts.Domain;
using RoyalIdentity.UserAccounts.Options;

namespace RoyalIdentity.UserAccounts.Features.Accounts.UseCases;

/// <summary>
/// User-facing "change my password" flow: an account changes its own local password after verifying the current
/// one. The new password is validated against the realm password complexity and reuse-history policies and the
/// change is persisted within the unit of work.
/// <para>
/// Unlike the seed/administrative <see cref="ChangeUserAccountPassword"/> (which deliberately ignores the toggle),
/// this use case enforces the realm user-facing policy <see cref="UserAccountsRealmOptions.AllowChangePassword"/>.
/// This plan delivers the use case + costura only; the HTTP/UI that drives it belongs to the admin/UI plan (Q12).
/// </para>
/// </summary>
public partial class ChangeOwnPassword
{
	/// <summary>
	/// Gets or sets the owning realm.
	/// </summary>
	public string RealmId { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the realm account policies (change-password toggle, complexity and reuse history).
	/// </summary>
	public UserAccountsRealmOptions Options { get; set; } = default!;

	/// <summary>
	/// Gets or sets the subject identifier of the account whose password changes.
	/// </summary>
	public string SubjectId { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the current plain password.
	/// </summary>
	public string CurrentPassword { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the new plain password.
	/// </summary>
	public string NewPassword { get; set; } = string.Empty;

	/// <summary>
	/// Validates the change-password input.
	/// </summary>
	/// <param name="problems">The collected problems, when invalid.</param>
	/// <returns><c>true</c> when the input is invalid.</returns>
	public bool HasProblems(out Problems? problems)
	{
		return Rules.Set<ChangeOwnPassword>()
			.NotNull(Options)
			.NotEmpty(RealmId)
			.NotEmpty(SubjectId)
			.NotEmpty(CurrentPassword)
			.NotEmpty(NewPassword)
			.HasProblems(out problems);
	}

	/// <summary>
	/// Executes the user-facing change-password use case.
	/// </summary>
	[Command, WithValidateModel, WithWorkContext]
	public async Task<Result> Execute(
		UserAccountReader reader,
		IUserAccountPasswordHasher passwordHasher,
		PasswordPolicy passwordPolicy,
		PasswordHistoryPolicy passwordHistoryPolicy,
		TimeProvider clock,
		CancellationToken ct)
	{
		if (!Options.AllowChangePassword)
		{
			return Problems.NotAllowed(
				"Changing the password is not allowed for this realm.",
				nameof(Options.AllowChangePassword),
				"user_account.change_password_not_allowed");
		}

		var account = await reader.FindBySubjectIdAsync(RealmId, SubjectId, ct);
		if (account is null)
		{
			return Problems.NotFound("Account was not found in the realm.", nameof(SubjectId), "user_account.not_found");
		}

		var now = clock.GetUtcNow();

		var policyResult = passwordPolicy.Validate(NewPassword, Options.PasswordOptions, account.Username);
		if (policyResult.HasProblems(out var passwordProblems))
		{
			return passwordProblems;
		}

		var currentPasswordResult = account.VerifyCurrentPassword(CurrentPassword, passwordHasher);
		if (currentPasswordResult.HasProblems(out var currentPasswordProblems))
		{
			return currentPasswordProblems;
		}

		var historyResult = passwordHistoryPolicy.Validate(NewPassword, account, Options.PasswordOptions, passwordHasher, now);
		if (historyResult.HasProblems(out var historyProblems))
		{
			return historyProblems;
		}

		return account.SetPassword(
			passwordHasher.Hash(NewPassword),
			now,
			Options.PasswordOptions,
			PasswordChangeReason.Change);
	}
}
