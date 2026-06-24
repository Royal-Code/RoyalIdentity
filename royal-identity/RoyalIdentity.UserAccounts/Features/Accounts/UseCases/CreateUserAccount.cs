using Microsoft.EntityFrameworkCore;
using RoyalCode.SmartCommands;
using RoyalCode.SmartProblems;
using RoyalCode.SmartValidations;
using RoyalIdentity.UserAccounts.Features.Accounts.Commons;
using RoyalIdentity.UserAccounts.Features.Accounts.Domain;
using RoyalIdentity.UserAccounts.Infrastructure.Data;
using RoyalIdentity.UserAccounts.Options;

namespace RoyalIdentity.UserAccounts.Features.Accounts.UseCases;

/// <summary>
/// Creates (or seeds) a local account for a realm. Input is normalized through the single normalization home,
/// the subject identifier is resolved by realm policy, realm-level uniqueness is enforced inside the unit of
/// work, and an optional fictitious email is materialized per realm policy.
/// <para>
/// This is a seed/administrative-level use case: realm user-facing toggles such as
/// <see cref="UserAccountsRealmOptions.AllowRegistration"/> are intentionally NOT enforced here (an operator or
/// seed may create accounts even when self-registration is disabled). The user-facing self-registration flow that
/// honors that policy belongs to the HTTP/UI layer.
/// </para>
/// </summary>
public partial class CreateUserAccount
{
	/// <summary>
	/// Gets or sets the owning realm.
	/// </summary>
	public string RealmId { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the realm account policies that drive this creation.
	/// </summary>
	public UserAccountsRealmOptions Options { get; set; } = default!;

	/// <summary>
	/// Gets or sets the username. Ignored when <see cref="UserAccountsRealmOptions.EmailAsUsername"/> is set.
	/// </summary>
	public string? Username { get; set; }

	/// <summary>
	/// Gets or sets the display name. Defaults to the username when omitted.
	/// </summary>
	public string? DisplayName { get; set; }

	/// <summary>
	/// Gets or sets an optional real email address.
	/// </summary>
	public string? Email { get; set; }

	/// <summary>
	/// Gets or sets whether the provided email is already verified.
	/// </summary>
	public bool EmailVerified { get; set; }

	/// <summary>
	/// Gets or sets an optional plain password to set on creation.
	/// </summary>
	public string? Password { get; set; }

	/// <summary>
	/// Gets or sets an optional caller-provided subject identifier. Honored only when the realm allows it.
	/// </summary>
	public string? SubjectId { get; set; }

	/// <summary>
	/// Gets or sets an optional external directory identifier. This is not a credential.
	/// </summary>
	public string? ExternalId { get; set; }

	/// <summary>
	/// Gets or sets optional roles to assign on creation.
	/// </summary>
	public IReadOnlyList<string>? Roles { get; set; }

	/// <summary>
	/// Validates the creation input against the realm policies.
	/// </summary>
	/// <param name="problems">The collected problems, when invalid.</param>
	/// <returns><c>true</c> when the input is invalid.</returns>
	public bool HasProblems(out Problems? problems)
	{
		var emailAsUsername = Options?.EmailAsUsername ?? false;

		return Rules.Set<CreateUserAccount>()
			.NotNull(Options)
			.NotEmpty(RealmId)
			.When(emailAsUsername, s => s
				.NotEmpty(Email)
				.Email(Email))
			.When(!emailAsUsername, s => s
				.NotEmpty(Username))
			.When(!emailAsUsername && !string.IsNullOrWhiteSpace(Email), s => s
				.Email(Email))
			.HasProblems(out problems);
	}

	/// <summary>
	/// Executes the creation use case.
	/// </summary>
	[Command, WithValidateModel, WithWorkContext]
	public async Task<Result<UserAccount>> Execute(
		UserAccountsDbContext db,
		IUserAccountNormalizer normalizer,
		ISubjectIdGenerator subjectIds,
		IUserAccountPasswordHasher passwordHasher,
		PasswordPolicy passwordPolicy,
		TimeProvider clock,
		CancellationToken ct)
	{
		var now = clock.GetUtcNow();

		var subjectResult = ResolveSubjectId(subjectIds);
		if (subjectResult.HasProblems(out var subjectProblems))
		{
			return subjectProblems;
		}

		subjectResult.HasValue(out var subjectId);

		var username = (Options.EmailAsUsername ? Email! : Username!).Trim();
		var normalizedUsername = normalizer.NormalizeUsername(username);
		var displayName = string.IsNullOrWhiteSpace(DisplayName) ? username : DisplayName.Trim();

		if (await db.UserAccounts.AnyAsync(a => a.RealmId == RealmId && a.SubjectId == subjectId, ct))
		{
			return Problems.InvalidState(
				"An account with this subject already exists in the realm.",
				nameof(SubjectId),
				"user_account.subject_duplicate");
		}

		if (await db.UserAccounts.AnyAsync(a => a.RealmId == RealmId && a.NormalizedUsername == normalizedUsername, ct))
		{
			return Problems.InvalidState(
				"An account with this username already exists in the realm.",
				nameof(Username),
				"user_account.username_duplicate");
		}

		string? passwordHash = null;
		if (!string.IsNullOrEmpty(Password))
		{
			var policyResult = passwordPolicy.Validate(Password, Options.PasswordOptions, username);
			if (policyResult.HasProblems(out var passwordProblems))
			{
				return passwordProblems;
			}

			passwordHash = passwordHasher.Hash(Password);
		}

		var account = new UserAccount(RealmId, subjectId!, username, normalizedUsername, displayName, now, ExternalId);

		var emailResult = await AddEmailAsync(db, account, normalizer, subjectId!, now, ct);
		if (emailResult.HasProblems(out var emailProblems))
		{
			return emailProblems;
		}

		var rolesResult = AddRoles(account, normalizer, now);
		if (rolesResult.HasProblems(out var roleProblems))
		{
			return roleProblems;
		}

		if (passwordHash is not null)
		{
			account.SetPassword(passwordHash, now, Options.PasswordOptions, PasswordChangeReason.Create);
		}

		db.UserAccounts.Add(account);
		return account;
	}

	private Result<string> ResolveSubjectId(ISubjectIdGenerator subjectIds)
	{
		if (string.IsNullOrWhiteSpace(SubjectId))
		{
			return subjectIds.NewSubjectId();
		}

		if (!Options.AllowProvidedSubjectId)
		{
			return Problems.NotAllowed(
				"Providing a subject identifier is not allowed for this realm.",
				nameof(SubjectId),
				"user_account.subject_not_allowed");
		}

		return SubjectId.Trim();
	}

	private async Task<Result> AddEmailAsync(
		UserAccountsDbContext db,
		UserAccount account,
		IUserAccountNormalizer normalizer,
		string subjectId,
		DateTimeOffset now,
		CancellationToken ct)
	{
		if (!string.IsNullOrWhiteSpace(Email))
		{
			var address = Email.Trim();
			var normalizedAddress = normalizer.NormalizeEmail(address);

			if (!Options.AllowDuplicateEmail &&
				await db.Set<UserAccountEmail>().AnyAsync(
					e => e.RealmId == RealmId && e.NormalizedAddress == normalizedAddress, ct))
			{
				return Problems.InvalidState(
					"An account with this email already exists in the realm.",
					nameof(Email),
					"user_account.email_duplicate");
			}

			var email = new UserAccountEmail(RealmId, address, normalizedAddress, isPrimary: true, isVerified: EmailVerified, isFictitious: false);
			return account.AddEmail(email, now);
		}

		if (Options.AllowFictitiousEmail)
		{
			var address = Options.FictitiousEmailPattern.Replace("{subjectId}", subjectId, StringComparison.Ordinal);
			var normalizedAddress = normalizer.NormalizeEmail(address);
			var email = new UserAccountEmail(
				RealmId,
				address,
				normalizedAddress,
				isPrimary: true,
				isVerified: Options.FictitiousEmailIsVerifiedByDefault,
				isFictitious: true);
			return account.AddEmail(email, now);
		}

		return Result.Ok();
	}

	private Result AddRoles(UserAccount account, IUserAccountNormalizer normalizer, DateTimeOffset now)
	{
		if (Roles is null)
		{
			return Result.Ok();
		}

		foreach (var roleName in Roles.Where(r => !string.IsNullOrWhiteSpace(r)))
		{
			var role = new UserAccountRole(RealmId, roleName.Trim(), normalizer.NormalizeRoleName(roleName));
			var result = account.AddRole(role, now);
			if (result.IsFailure)
			{
				return result;
			}
		}

		return Result.Ok();
	}
}
