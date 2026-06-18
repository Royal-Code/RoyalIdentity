using RoyalCode.Aggregates;
using RoyalCode.SmartProblems;
using RoyalIdentity.UserAccounts.Options;

namespace RoyalIdentity.UserAccounts.Features.Accounts.Domain;

/// <summary>
/// Rich account aggregate owned by the UserAccounts module.
/// </summary>
public class UserAccount : AggregateRoot<long>
{
	private readonly List<UserAccountEmail> emails = [];
	private readonly List<UserAccountRole> roles = [];

	private UserAccount()
	{
	}

	private UserAccount(
		string realmId,
		string subjectId,
		string username,
		string displayName,
		string? externalId,
		DateTimeOffset createdAt)
	{
		RealmId = realmId;
		SubjectId = subjectId;
		Username = username;
		NormalizedUsername = Normalize(username);
		DisplayName = displayName;
		ExternalId = NormalizeOptional(externalId);
		Status = AccountStatus.Active;
		CreatedAt = createdAt;
		UpdatedAt = createdAt;

		AddEvent(new UserAccountCreated(RealmId, SubjectId, Username));
	}

	/// <summary>
	/// Gets the realm that owns this account.
	/// </summary>
	public string RealmId { get; private set; } = string.Empty;

	/// <summary>
	/// Gets the immutable OIDC subject identifier.
	/// </summary>
	public string SubjectId { get; private set; } = string.Empty;

	/// <summary>
	/// Gets the account username.
	/// </summary>
	public string Username { get; private set; } = string.Empty;

	/// <summary>
	/// Gets the normalized username used for lookup.
	/// </summary>
	public string NormalizedUsername { get; private set; } = string.Empty;

	/// <summary>
	/// Gets the display name projected into profile claims.
	/// </summary>
	public string DisplayName { get; private set; } = string.Empty;

	/// <summary>
	/// Gets the account lifecycle status.
	/// </summary>
	public AccountStatus Status { get; private set; }

	/// <summary>
	/// Gets an optional external directory identifier. This is not a credential.
	/// </summary>
	public string? ExternalId { get; private set; }

	/// <summary>
	/// Gets the creation timestamp.
	/// </summary>
	public DateTimeOffset CreatedAt { get; private set; }

	/// <summary>
	/// Gets the last mutation timestamp.
	/// </summary>
	public DateTimeOffset UpdatedAt { get; private set; }

	/// <summary>
	/// Gets account emails.
	/// </summary>
	public IReadOnlyCollection<UserAccountEmail> Emails => emails;

	/// <summary>
	/// Gets account roles.
	/// </summary>
	public IReadOnlyCollection<UserAccountRole> Roles => roles;

	/// <summary>
	/// Gets the local password credential state.
	/// </summary>
	public UserAccountCredential LocalCredential { get; private set; } = new();

	/// <summary>
	/// Gets the current primary email, when one exists.
	/// </summary>
	public UserAccountEmail? PrimaryEmail => emails.FirstOrDefault(e => e.IsPrimary);

	/// <summary>
	/// Creates a new user account with generated or policy-approved subject id.
	/// </summary>
	/// <param name="realmId">The owning realm.</param>
	/// <param name="username">The account username.</param>
	/// <param name="displayName">The display name.</param>
	/// <param name="options">The realm-scoped UserAccounts policies.</param>
	/// <param name="createdAt">The creation timestamp.</param>
	/// <param name="subjectId">Optional externally supplied subject id.</param>
	/// <param name="externalId">Optional external directory identifier.</param>
	/// <param name="subjectIdGenerator">Optional generator used when <paramref name="subjectId"/> is absent.</param>
	/// <returns>A successful result with the account or a problem result for invalid input/policy.</returns>
	public static Result<UserAccount> Create(
		string realmId,
		string username,
		string displayName,
		UserAccountsRealmOptions options,
		DateTimeOffset createdAt,
		string? subjectId = null,
		string? externalId = null,
		Func<string>? subjectIdGenerator = null)
	{
		ArgumentNullException.ThrowIfNull(options);

		var realm = NormalizeRequired(realmId);
		if (realm is null)
		{
			return InvalidParameter("RealmId is required.", nameof(realmId), "user_account.realm_required")
				.AsResult<UserAccount>();
		}

		var user = NormalizeRequired(username);
		if (user is null)
		{
			return InvalidParameter("Username is required.", nameof(username), "user_account.username_required")
				.AsResult<UserAccount>();
		}

		var name = NormalizeRequired(displayName);
		if (name is null)
		{
			return InvalidParameter("DisplayName is required.", nameof(displayName), "user_account.display_name_required")
				.AsResult<UserAccount>();
		}

		var sub = NormalizeRequired(subjectId);
		if (sub is not null && !options.AllowProvidedSubjectId)
		{
			return NotAllowed(
					"Providing SubjectId is not allowed by realm policy.",
					nameof(subjectId),
					"user_account.subject_id_not_allowed")
				.AsResult<UserAccount>();
		}

		sub ??= NormalizeRequired((subjectIdGenerator ?? SubjectIdGenerator.Create)());
		if (sub is null)
		{
			return InvalidParameter("SubjectId generator returned an empty value.", nameof(subjectIdGenerator), "user_account.subject_id_required")
				.AsResult<UserAccount>();
		}

		return new UserAccount(realm, sub, user, name, externalId, createdAt);
	}

	/// <summary>
	/// Changes the username without changing the subject identifier.
	/// </summary>
	/// <param name="username">The new username.</param>
	/// <param name="changedAt">The mutation timestamp.</param>
	/// <returns>A result describing whether the change succeeded.</returns>
	public Result ChangeUsername(string username, DateTimeOffset changedAt)
	{
		var value = NormalizeRequired(username);
		if (value is null)
		{
			return InvalidParameter("Username is required.", nameof(username), "user_account.username_required");
		}

		if (NormalizedUsername == Normalize(value))
		{
			return new Result();
		}

		Username = value;
		NormalizedUsername = Normalize(value);
		Touch(changedAt);
		AddEvent(new UserAccountUsernameChanged(RealmId, SubjectId, Username));
		return new Result();
	}

	/// <summary>
	/// Changes the display name.
	/// </summary>
	/// <param name="displayName">The new display name.</param>
	/// <param name="changedAt">The mutation timestamp.</param>
	/// <returns>A result describing whether the change succeeded.</returns>
	public Result ChangeDisplayName(string displayName, DateTimeOffset changedAt)
	{
		var value = NormalizeRequired(displayName);
		if (value is null)
		{
			return InvalidParameter("DisplayName is required.", nameof(displayName), "user_account.display_name_required");
		}

		DisplayName = value;
		Touch(changedAt);
		return new Result();
	}

	/// <summary>
	/// Stores or clears an external identifier.
	/// </summary>
	/// <param name="externalId">The external identifier.</param>
	/// <param name="changedAt">The mutation timestamp.</param>
	public void SetExternalId(string? externalId, DateTimeOffset changedAt)
	{
		ExternalId = NormalizeOptional(externalId);
		Touch(changedAt);
	}

	/// <summary>
	/// Adds an email to the account.
	/// </summary>
	/// <param name="address">The email address.</param>
	/// <param name="changedAt">The mutation timestamp.</param>
	/// <param name="isPrimary">Whether this email should be primary.</param>
	/// <param name="isVerified">Whether this email is verified.</param>
	/// <param name="isFictitious">Whether this email is fictitious.</param>
	/// <returns>A result describing whether the email was added.</returns>
	public Result AddEmail(
		string address,
		DateTimeOffset changedAt,
		bool isPrimary = false,
		bool isVerified = false,
		bool isFictitious = false)
	{
		var value = NormalizeRequired(address);
		if (value is null)
		{
			return InvalidParameter("Email address is required.", nameof(address), "user_account.email_required");
		}

		var normalized = UserAccountEmail.Normalize(value);
		if (emails.Any(e => e.NormalizedAddress == normalized))
		{
			return InvalidState("Email already exists in this account.", nameof(address), "user_account.email_duplicate");
		}

		var shouldBePrimary = isPrimary || emails.Count is 0;
		if (shouldBePrimary)
		{
			ClearPrimaryEmail();
		}

		var email = new UserAccountEmail(value, shouldBePrimary, isVerified, isFictitious);
		emails.Add(email);
		Touch(changedAt);
		AddEvent(new UserAccountEmailAdded(RealmId, SubjectId, email.Address, email.IsPrimary));

		if (email.IsPrimary)
		{
			AddEvent(new UserAccountPrimaryEmailChanged(RealmId, SubjectId, email.Address));
		}

		return new Result();
	}

	/// <summary>
	/// Changes the primary email.
	/// </summary>
	/// <param name="address">The email address to make primary.</param>
	/// <param name="changedAt">The mutation timestamp.</param>
	/// <returns>A result describing whether the change succeeded.</returns>
	public Result ChangePrimaryEmail(string address, DateTimeOffset changedAt)
	{
		var value = NormalizeRequired(address);
		if (value is null)
		{
			return InvalidParameter("Email address is required.", nameof(address), "user_account.email_required");
		}

		var normalized = UserAccountEmail.Normalize(value);
		var email = emails.FirstOrDefault(e => e.NormalizedAddress == normalized);
		if (email is null)
		{
			return InvalidState("Email does not exist in this account.", nameof(address), "user_account.email_missing");
		}

		ClearPrimaryEmail();
		email.MarkPrimary(true);
		Touch(changedAt);
		AddEvent(new UserAccountPrimaryEmailChanged(RealmId, SubjectId, email.Address));
		return new Result();
	}

	/// <summary>
	/// Adds a role directly to the account.
	/// </summary>
	/// <param name="role">The role name.</param>
	/// <param name="changedAt">The mutation timestamp.</param>
	/// <returns>A result describing whether the role was added.</returns>
	public Result AddRole(string role, DateTimeOffset changedAt)
	{
		var value = NormalizeRequired(role);
		if (value is null)
		{
			return InvalidParameter("Role is required.", nameof(role), "user_account.role_required");
		}

		var normalized = UserAccountRole.Normalize(value);
		if (roles.Any(r => r.NormalizedName == normalized))
		{
			return InvalidState("Role already exists in this account.", nameof(role), "user_account.role_duplicate");
		}

		var accountRole = new UserAccountRole(value);
		roles.Add(accountRole);
		Touch(changedAt);
		AddEvent(new UserAccountRoleAdded(RealmId, SubjectId, accountRole.Name));
		return new Result();
	}

	/// <summary>
	/// Removes a role directly from the account.
	/// </summary>
	/// <param name="role">The role name.</param>
	/// <param name="changedAt">The mutation timestamp.</param>
	/// <returns>A result describing whether the role was removed.</returns>
	public Result RemoveRole(string role, DateTimeOffset changedAt)
	{
		var value = NormalizeRequired(role);
		if (value is null)
		{
			return InvalidParameter("Role is required.", nameof(role), "user_account.role_required");
		}

		var normalized = UserAccountRole.Normalize(value);
		var accountRole = roles.FirstOrDefault(r => r.NormalizedName == normalized);
		if (accountRole is null)
		{
			return new Result();
		}

		roles.Remove(accountRole);
		Touch(changedAt);
		AddEvent(new UserAccountRoleRemoved(RealmId, SubjectId, accountRole.Name));
		return new Result();
	}

	/// <summary>
	/// Sets or replaces the local password credential.
	/// </summary>
	/// <param name="password">The new plain password.</param>
	/// <param name="options">The password policy.</param>
	/// <param name="passwordHasher">The password hasher.</param>
	/// <param name="changedAt">The mutation timestamp.</param>
	/// <param name="mustChangePassword">Whether the user must change the password later.</param>
	/// <returns>A result describing whether the password was stored.</returns>
	public Result SetPassword(
		string password,
		PasswordOptions options,
		IUserAccountPasswordHasher passwordHasher,
		DateTimeOffset changedAt,
		bool mustChangePassword = false)
	{
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(passwordHasher);

		var validation = ValidatePassword(password, options);
		if (validation.IsFailure)
		{
			return validation;
		}

		LocalCredential.SetPassword(passwordHasher.Hash(password), changedAt, mustChangePassword);
		Touch(changedAt);
		AddEvent(new UserAccountPasswordChanged(RealmId, SubjectId));
		return new Result();
	}

	/// <summary>
	/// Changes the local password after verifying the current password.
	/// </summary>
	/// <param name="currentPassword">The current plain password.</param>
	/// <param name="newPassword">The new plain password.</param>
	/// <param name="options">The password policy.</param>
	/// <param name="passwordHasher">The password hasher.</param>
	/// <param name="changedAt">The mutation timestamp.</param>
	/// <returns>A result describing whether the password was changed.</returns>
	public Result ChangePassword(
		string currentPassword,
		string newPassword,
		PasswordOptions options,
		IUserAccountPasswordHasher passwordHasher,
		DateTimeOffset changedAt)
	{
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(passwordHasher);

		if (!LocalCredential.HasPassword)
		{
			return InvalidState("Account does not have a local password.", nameof(currentPassword), "user_account.password_not_set");
		}

		if (!passwordHasher.Verify(currentPassword, LocalCredential.PasswordHash!))
		{
			return InvalidParameter("Current password is invalid.", nameof(currentPassword), "user_account.current_password_invalid");
		}

		return SetPassword(newPassword, options, passwordHasher, changedAt);
	}

	/// <summary>
	/// Authenticates a local password and updates failed-attempt state.
	/// </summary>
	/// <param name="password">The plain password supplied by the caller.</param>
	/// <param name="options">The password and lockout policy.</param>
	/// <param name="passwordHasher">The password hasher.</param>
	/// <param name="attemptedAt">The authentication timestamp.</param>
	/// <returns>The authentication outcome.</returns>
	public LocalAuthenticationResult AuthenticateLocal(
		string password,
		PasswordOptions options,
		IUserAccountPasswordHasher passwordHasher,
		DateTimeOffset attemptedAt)
	{
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(passwordHasher);

		if (Status is AccountStatus.Inactive)
		{
			return LocalAuthenticationResult.Failed(LocalAuthenticationFailureReason.Inactive);
		}

		if (Status is AccountStatus.Blocked)
		{
			return LocalAuthenticationResult.Failed(LocalAuthenticationFailureReason.Blocked);
		}

		if (!LocalCredential.HasPassword)
		{
			return LocalAuthenticationResult.Failed(LocalAuthenticationFailureReason.PasswordNotSet);
		}

		LocalCredential.ClearExpiredLockout(options, attemptedAt);
		if (LocalCredential.IsLockedOut(options, attemptedAt))
		{
			return LocalAuthenticationResult.Failed(
				LocalAuthenticationFailureReason.LockedOut,
				LocalCredential.LockoutEndAt);
		}

		if (!passwordHasher.Verify(password, LocalCredential.PasswordHash!))
		{
			var locked = LocalCredential.RegisterFailure(options, attemptedAt);
			Touch(attemptedAt);

			if (locked)
			{
				AddEvent(new UserAccountLocalCredentialLocked(RealmId, SubjectId, LocalCredential.LockoutEndAt));
			}

			return LocalAuthenticationResult.Failed(LocalAuthenticationFailureReason.InvalidCredentials);
		}

		LocalCredential.ResetFailures();
		Touch(attemptedAt);
		return LocalAuthenticationResult.Succeeded(this);
	}

	/// <summary>
	/// Marks the account as active.
	/// </summary>
	/// <param name="changedAt">The mutation timestamp.</param>
	public void Activate(DateTimeOffset changedAt)
	{
		ChangeStatus(AccountStatus.Active, changedAt);
	}

	/// <summary>
	/// Marks the account as inactive.
	/// </summary>
	/// <param name="changedAt">The mutation timestamp.</param>
	public void Deactivate(DateTimeOffset changedAt)
	{
		ChangeStatus(AccountStatus.Inactive, changedAt);
	}

	/// <summary>
	/// Blocks the account.
	/// </summary>
	/// <param name="changedAt">The mutation timestamp.</param>
	public void Block(DateTimeOffset changedAt)
	{
		ChangeStatus(AccountStatus.Blocked, changedAt);
	}

	/// <summary>
	/// Clears local credential lockout counters.
	/// </summary>
	/// <param name="changedAt">The mutation timestamp.</param>
	public void UnlockLocalCredential(DateTimeOffset changedAt)
	{
		LocalCredential.ResetFailures();
		Touch(changedAt);
	}

	private Result ValidatePassword(string password, PasswordOptions options)
	{
		if (string.IsNullOrEmpty(password))
		{
			return InvalidParameter("Password is required.", nameof(password), "user_account.password_required");
		}

		if (password.Length < options.MinimumLength)
		{
			return InvalidParameter("Password is shorter than the minimum length.", nameof(password), "user_account.password_too_short");
		}

		if (password.Length > options.MaximumLength)
		{
			return InvalidParameter("Password is longer than the maximum length.", nameof(password), "user_account.password_too_long");
		}

		if (options.RequireDigit && !password.Any(char.IsDigit))
		{
			return InvalidParameter("Password must contain a digit.", nameof(password), "user_account.password_requires_digit");
		}

		if (options.RequireLowercase && !password.Any(char.IsLower))
		{
			return InvalidParameter("Password must contain a lowercase letter.", nameof(password), "user_account.password_requires_lowercase");
		}

		if (options.RequireUppercase && !password.Any(char.IsUpper))
		{
			return InvalidParameter("Password must contain an uppercase letter.", nameof(password), "user_account.password_requires_uppercase");
		}

		if (options.RequireSpecialCharacters && password.All(char.IsLetterOrDigit))
		{
			return InvalidParameter("Password must contain a special character.", nameof(password), "user_account.password_requires_special");
		}

		if (options.MinimumUniqueCharacters > 0 &&
			password.Distinct().Count() < options.MinimumUniqueCharacters)
		{
			return InvalidParameter("Password does not contain enough unique characters.", nameof(password), "user_account.password_unique_chars");
		}

		if (options.DisallowUsernameInPassword &&
			password.Contains(Username, StringComparison.OrdinalIgnoreCase))
		{
			return InvalidParameter("Password cannot contain the username.", nameof(password), "user_account.password_contains_username");
		}

		foreach (var disallowedWord in options.DisallowedWordsInPassword)
		{
			if (!string.IsNullOrWhiteSpace(disallowedWord) &&
				password.Contains(disallowedWord, StringComparison.OrdinalIgnoreCase))
			{
				return InvalidParameter("Password contains a disallowed word.", nameof(password), "user_account.password_disallowed_word");
			}
		}

		return new Result();
	}

	private void ChangeStatus(AccountStatus status, DateTimeOffset changedAt)
	{
		if (Status == status)
		{
			return;
		}

		Status = status;
		Touch(changedAt);
		AddEvent(new UserAccountStatusChanged(RealmId, SubjectId, status));
	}

	private void ClearPrimaryEmail()
	{
		foreach (var email in emails.Where(e => e.IsPrimary))
		{
			email.MarkPrimary(false);
		}
	}

	private void Touch(DateTimeOffset changedAt)
	{
		UpdatedAt = changedAt;
	}

	private static string Normalize(string value)
	{
		return value.Trim().ToUpperInvariant();
	}

	private static string? NormalizeRequired(string? value)
	{
		var trimmed = value?.Trim();
		return string.IsNullOrEmpty(trimmed) ? null : trimmed;
	}

	private static string? NormalizeOptional(string? value)
	{
		var trimmed = value?.Trim();
		return string.IsNullOrEmpty(trimmed) ? null : trimmed;
	}

	private static Problem InvalidParameter(string detail, string property, string typeId)
	{
		return Problems.InvalidParameter(detail, property, typeId);
	}

	private static Problem InvalidState(string detail, string property, string typeId)
	{
		return Problems.InvalidState(detail, property, typeId);
	}

	private static Problem NotAllowed(string detail, string property, string typeId)
	{
		return Problems.NotAllowed(detail, property, typeId);
	}
}
