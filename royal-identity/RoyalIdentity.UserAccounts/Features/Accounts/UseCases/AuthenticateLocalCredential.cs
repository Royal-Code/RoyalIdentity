using RoyalCode.SmartCommands;
using RoyalCode.SmartCommands.WorkContext.Options;
using RoyalCode.SmartProblems;
using RoyalCode.SmartValidations;
using RoyalCode.UnitOfWork;
using RoyalCode.WorkContext;
using RoyalIdentity.UserAccounts.Features.Accounts.Commons;
using RoyalIdentity.UserAccounts.Features.Accounts.Domain;
using RoyalIdentity.UserAccounts.Options;

namespace RoyalIdentity.UserAccounts.Features.Accounts.UseCases;

/// <summary>
/// Authenticates a local credential by login. Resolves the account by the realm's login policy, applies the
/// domain authentication rules (active, blocked, password set, lockout, credentials) and persists the updated
/// failed-attempt/lockout state. The internal failure reason is preserved for the integration edge to map to a
/// generic external error.
/// <para>
/// Deliberately excluded from optimistic-concurrency retry (Q4 — best-effort): the hot-path cost of retrying on a
/// contended login (e.g. brute-force) was judged not worth it; under an exact race, the failed-attempt counter may
/// undercount by one (lockout fires 1-2 attempts later — acceptable, it is a throttle, not a gate).
/// </para>
/// <para>
/// The unit of work is committed manually (not by a <c>[WithWorkContext]</c>-generated accessor) so a genuine
/// conflict on save can be caught <em>here</em>, in the module, and turned into a controlled <see cref="Problem"/> —
/// never left to leak a raw <see cref="ConcurrencyException"/> to the <c>.Integration</c> edge (which must not know
/// about <c>WorkContext</c>/EF concurrency types per the ADR-013 module boundary). Fail-closed: the outcome computed
/// against the (now possibly stale) account is discarded on conflict — between the read and the save the account
/// could have been blocked, deactivated, or had its password changed by another request.
/// </para>
/// </summary>
public partial class AuthenticateLocalCredential
{
	/// <summary>
	/// Gets or sets the owning realm.
	/// </summary>
	public string RealmId { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the realm account policies (login resolution and lockout).
	/// </summary>
	public UserAccountsRealmOptions Options { get; set; } = default!;

	/// <summary>
	/// Gets or sets the raw login (username or email).
	/// </summary>
	public string Login { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the plain password supplied by the caller.
	/// </summary>
	public string Password { get; set; } = string.Empty;

	/// <summary>
	/// Validates the authentication input.
	/// </summary>
	/// <param name="problems">The collected problems, when invalid.</param>
	/// <returns><c>true</c> when the input is invalid.</returns>
	public bool HasProblems(out Problems? problems)
	{
		return Rules.Set<AuthenticateLocalCredential>()
			.NotNull(Options)
			.NotEmpty(RealmId)
			.NotEmpty(Login)
			.NotEmpty(Password)
			.HasProblems(out problems);
	}

	/// <summary>
	/// Executes the authentication use case.
	/// </summary>
	[Command, WithValidateModel]
	public async Task<Result<LocalAuthenticationResult>> Execute(
		IWorkContext work,
		UserAccountReader reader,
		IUserAccountPasswordHasher passwordHasher,
		TimeProvider clock,
		CancellationToken ct)
	{
		// MaxAttempts = 1 deliberately means no retry (Q4). Reusing the concurrency primitive still guarantees
		// rollback of a current transaction and change-tracker cleanup before the controlled fail-closed problem
		// is returned, so the scoped WorkContext is not left holding a stale modified aggregate.
		return await work.RetryOnConcurrencyAsync<LocalAuthenticationResult>(
			async () =>
			{
				var account = await reader.FindByLoginAsync(RealmId, Login, Options, ct);
				if (account is null)
				{
					return LocalAuthenticationResult.Failed(LocalAuthenticationFailureReason.NotFound);
				}

				var outcome = account.AuthenticateLocal(
					Password,
					Options.PasswordOptions,
					passwordHasher,
					clock.GetUtcNow());

				return (await work.SaveAsync(ct)).Map(outcome);
			},
			new RetryOnConcurrencyOptions { MaxAttempts = 1 },
			onExhausted: () => Problems.InvalidState(
				ConcurrencyRetryExtensions.ConcurrencyConflictDetail,
				typeId: "user_account.concurrency_conflict"),
			ct: ct);
	}
}
