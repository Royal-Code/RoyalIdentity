using RoyalCode.SmartCommands;
using RoyalCode.SmartProblems;
using RoyalCode.SmartValidations;
using RoyalIdentity.UserAccounts.Features.Accounts.Commons;

namespace RoyalIdentity.UserAccounts.Features.Accounts.UseCases;

/// <summary>
/// Administrative clear of a user account block (ADR-017 §2.5). Removes any administrative block (including a
/// scheduled or still-active window); it does not touch the credential lockout (<see cref="UnlockPasswordCredential"/>),
/// which is a distinct state.
/// </summary>
public partial class UnblockUserAccount
{
	/// <summary>
	/// Gets or sets the owning realm.
	/// </summary>
	public string RealmId { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the subject identifier of the account to unblock.
	/// </summary>
	public string SubjectId { get; set; } = string.Empty;

	/// <summary>
	/// Validates the unblock input.
	/// </summary>
	/// <param name="problems">The collected problems, when invalid.</param>
	/// <returns><c>true</c> when the input is invalid.</returns>
	public bool HasProblems(out Problems? problems)
	{
		return Rules.Set<UnblockUserAccount>()
			.NotEmpty(RealmId)
			.NotEmpty(SubjectId)
			.HasProblems(out problems);
	}

	/// <summary>
	/// Executes the administrative unblock.
	/// </summary>
	[Command, WithValidateModel, WithWorkContext]
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

		account.Unblock(clock.GetUtcNow());
		return Result.Ok();
	}
}
