using RoyalCode.SmartCommands;
using RoyalCode.SmartProblems;
using RoyalCode.SmartValidations;
using RoyalIdentity.UserAccounts.Features.Accounts.Commons;

namespace RoyalIdentity.UserAccounts.Features.Accounts.UseCases;

/// <summary>
/// Administrative block of a user account (ADR-017 §2.5). The block can be effective immediately and indefinitely
/// (the common case) or scheduled to a time window via <see cref="StartsAt"/>/<see cref="EndsAt"/> (e.g. blocking an
/// account while its owner is on vacation). This is a distinct state from the credential lockout; the window is
/// enforced at authentication time. Window validity is checked here (in the feature), not in the aggregate.
/// </summary>
public partial class BlockUserAccount
{
	/// <summary>
	/// Gets or sets the owning realm.
	/// </summary>
	public string RealmId { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the subject identifier of the account to block.
	/// </summary>
	public string SubjectId { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the optional block reason.
	/// </summary>
	public string? Reason { get; set; }

	/// <summary>
	/// Gets or sets when the block becomes effective. <c>null</c> means immediately.
	/// </summary>
	public DateTimeOffset? StartsAt { get; set; }

	/// <summary>
	/// Gets or sets when the block expires. <c>null</c> means indefinite.
	/// </summary>
	public DateTimeOffset? EndsAt { get; set; }

	/// <summary>
	/// Validates the block input.
	/// </summary>
	/// <param name="problems">The collected problems, when invalid.</param>
	/// <returns><c>true</c> when the input is invalid.</returns>
	public bool HasProblems(out Problems? problems)
	{
		return Rules.Set<BlockUserAccount>()
			.NotEmpty(RealmId)
			.NotEmpty(SubjectId)
			.HasProblems(out problems);
	}

	/// <summary>
	/// Executes the administrative block.
	/// </summary>
	[Command, WithValidateModel, WithWorkContext]
	public async Task<Result> Execute(
		UserAccountReader reader,
		TimeProvider clock,
		CancellationToken ct)
	{
		var now = clock.GetUtcNow();

		// A closed window must be ordered, and a block that would already be expired is meaningless. The effective
		// start is the supplied StartsAt or now (an immediate block).
		if (EndsAt is not null)
		{
			var effectiveStart = StartsAt ?? now;
			if (EndsAt.Value <= effectiveStart || EndsAt.Value <= now)
			{
				return Problems.InvalidParameter(
					"The block window end must be after its start and still be in the future.",
					nameof(EndsAt),
					"user_account.block_window_invalid");
			}
		}

		var account = await reader.FindBySubjectIdAsync(RealmId, SubjectId, ct);
		if (account is null)
		{
			return Problems.NotFound("Account was not found in the realm.", nameof(SubjectId), "user_account.not_found");
		}

		account.Block(Reason, now, StartsAt, EndsAt);
		return Result.Ok();
	}
}
