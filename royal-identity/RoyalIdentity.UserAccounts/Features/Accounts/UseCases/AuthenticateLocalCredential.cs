using RoyalCode.SmartCommands;
using RoyalCode.SmartProblems;
using RoyalCode.SmartValidations;
using RoyalIdentity.UserAccounts.Features.Accounts.Commons;
using RoyalIdentity.UserAccounts.Features.Accounts.Domain;
using RoyalIdentity.UserAccounts.Options;

namespace RoyalIdentity.UserAccounts.Features.Accounts.UseCases;

/// <summary>
/// Authenticates a local credential by login. Resolves the account by the realm's login policy, applies the
/// domain authentication rules (active, blocked, password set, lockout, credentials) and persists the updated
/// failed-attempt/lockout state. The internal failure reason is preserved for the integration edge to map to a
/// generic external error.
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
	[Command, WithValidateModel, WithWorkContext]
	public async Task<Result<LocalAuthenticationResult>> Execute(
		UserAccountReader reader,
		IUserAccountPasswordHasher passwordHasher,
		TimeProvider clock,
		CancellationToken ct)
	{
		var account = await reader.FindByLoginAsync(RealmId, Login, Options, ct);
		if (account is null)
		{
			return LocalAuthenticationResult.Failed(LocalAuthenticationFailureReason.NotFound);
		}

		return account.AuthenticateLocal(Password, Options.PasswordOptions, passwordHasher, clock.GetUtcNow());
	}
}
