using RoyalCode.SmartCommands;
using RoyalCode.SmartProblems;
using RoyalCode.SmartValidations;
using RoyalIdentity.UserAccounts.Features.Accounts.Commons;

namespace RoyalIdentity.UserAccounts.Features.Accounts.UseCases;

/// <summary>
/// Administrative unlock of the local password credential (ADR-017 §2.5). Clears the failed-attempt counter and any
/// active temporary lockout — including an indefinite lockout, which is produced when the realm sets no lockout
/// duration and therefore can only be cleared administratively (or by a password change). This targets the credential
/// lockout only; it does not touch the administrative block (<see cref="UnblockUserAccount"/>), which is a distinct
/// state.
/// </summary>
public partial class UnlockPasswordCredential
{
	/// <summary>
	/// Gets or sets the owning realm.
	/// </summary>
	public string RealmId { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the subject identifier of the account whose credential is unlocked.
	/// </summary>
	public string SubjectId { get; set; } = string.Empty;

	/// <summary>
	/// Validates the unlock input.
	/// </summary>
	/// <param name="problems">The collected problems, when invalid.</param>
	/// <returns><c>true</c> when the input is invalid.</returns>
	public bool HasProblems(out Problems? problems)
	{
		return Rules.Set<UnlockPasswordCredential>()
			.NotEmpty(RealmId)
			.NotEmpty(SubjectId)
			.HasProblems(out problems);
	}

	/// <summary>
	/// Executes the administrative credential unlock.
	/// </summary>
	[Command, WithValidateModel, WithWorkContext, WithRetryOnConcurrency]
	public async Task<Result> Execute(
		UserAccountReader reader,
		TimeProvider clock,
		CancellationToken ct)
	{
		var account = await reader.FindBySubjectIdAsync(RealmId, SubjectId, ct);
		if (account is null)
		{
			return Problems.NotFound("Account was not found in the realm.", nameof(SubjectId), "user_account.not_found");
		}

		account.UnlockLocalCredential(clock.GetUtcNow());
		return Result.Ok();
	}
}
