using RoyalCode.Aggregates;
using RoyalCode.SmartProblems;
using RoyalIdentity.UserAccounts.Features.ScopeProperties.Domain;
using RoyalIdentity.UserAccounts.Options;

namespace RoyalIdentity.UserAccounts.Features.Accounts.Domain;

/// <summary>
/// Rich account aggregate owned by the UserAccounts module.
/// </summary>
public class UserAccount : AggregateRoot<long>
{
	private List<UserAccountEmail> emails = [];
	private List<UserAccountPropertyValue> propertyValues = [];
	private List<UserAccountRole> roles = [];

#nullable disable
	/// <summary>
	/// Constructor for EF Core.
	/// </summary>
	protected UserAccount()
	{
	}
#nullable restore

	/// <summary>
	/// Creates a new user account from already validated and normalized values.
	/// </summary>
	/// <param name="realmId">The owning realm.</param>
	/// <param name="subjectId">The immutable subject identifier.</param>
	/// <param name="username">The account username.</param>
	/// <param name="normalizedUsername">The normalized username.</param>
	/// <param name="displayName">The display name.</param>
	/// <param name="createdAt">The creation timestamp.</param>
	/// <param name="externalId">Optional external directory identifier.</param>
	public UserAccount(
		string realmId,
		string subjectId,
		string username,
		string normalizedUsername,
		string displayName,
		DateTimeOffset createdAt,
		string? externalId = null)
	{
		RealmId = realmId;
		SubjectId = subjectId;
		Username = username;
		NormalizedUsername = normalizedUsername;
		DisplayName = displayName;
		ExternalId = externalId;
		IsActive = true;
		BlockState = UserAccountBlockState.Unblocked();
		CreatedAt = createdAt;
		UpdatedAt = createdAt;
		LocalCredential = new UserAccountCredential(RealmId);
		LocalCredential.AttachTo(this);

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
	/// Gets whether the account is enabled by lifecycle rules.
	/// </summary>
	public bool IsActive { get; private set; }

	/// <summary>
	/// Gets whether the account is administratively blocked.
	/// </summary>
	public bool IsBlocked => BlockState.IsBlocked;

	/// <summary>
	/// Gets why the account was administratively blocked.
	/// </summary>
	public string? BlockedReason => BlockState.BlockedReason;

	/// <summary>
	/// Gets when the account was administratively blocked.
	/// </summary>
	public DateTimeOffset? BlockedAt => BlockState.BlockedAt;

	/// <summary>
	/// Gets the administrative block state.
	/// </summary>
	public UserAccountBlockState BlockState { get; private set; } = UserAccountBlockState.Unblocked();

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
	/// Gets the provider-specific concurrency token.
	/// </summary>
	public uint Version { get; private set; }

	/// <summary>
	/// Gets the local password credential state.
	/// </summary>
	public virtual UserAccountCredential LocalCredential { get; private set; } = default!;

	/// <summary>
	/// Gets account emails.
	/// </summary>
	public IReadOnlyCollection<UserAccountEmail> Emails => emails;

	/// <summary>
	/// Gets account roles.
	/// </summary>
	public IReadOnlyCollection<UserAccountRole> Roles => roles;

	/// <summary>
	/// Gets dynamic property values assigned to this account.
	/// </summary>
	public IReadOnlyCollection<UserAccountPropertyValue> PropertyValues => propertyValues;

	/// <summary>
	/// Gets the current primary email, when one exists.
	/// </summary>
	public UserAccountEmail? PrimaryEmail => EmailItems.FirstOrDefault(e => e.IsPrimary);

	/// <summary>
	/// Collection navigation used by EF Core mapping while public access remains read-only.
	/// </summary>
	protected virtual List<UserAccountEmail> EmailItems
	{
		get => emails;
		set => emails = value;
	}

	/// <summary>
	/// Collection navigation used by EF Core mapping while public access remains read-only.
	/// </summary>
	protected virtual List<UserAccountRole> RoleItems
	{
		get => roles;
		set => roles = value;
	}

	/// <summary>
	/// Collection navigation used by EF Core mapping while public access remains read-only.
	/// </summary>
	protected virtual List<UserAccountPropertyValue> PropertyValueItems
	{
		get => propertyValues;
		set => propertyValues = value;
	}

	/// <summary>
	/// Changes the username without changing the subject identifier.
	/// </summary>
	/// <param name="username">The new username.</param>
	/// <param name="normalizedUsername">The new normalized username.</param>
	/// <param name="changedAt">The mutation timestamp.</param>
	/// <returns>A result describing whether the change succeeded.</returns>
	public Result ChangeUsername(string username, string normalizedUsername, DateTimeOffset changedAt)
	{
		if (NormalizedUsername == normalizedUsername)
		{
			return Result.Ok();
		}

		Username = username;
		NormalizedUsername = normalizedUsername;
		Touch(changedAt);
		AddEvent(new UserAccountUsernameChanged(RealmId, SubjectId, Username));
		return Result.Ok();
	}

	/// <summary>
	/// Changes the display name.
	/// </summary>
	/// <param name="displayName">The new display name.</param>
	/// <param name="changedAt">The mutation timestamp.</param>
	/// <returns>A result describing whether the change succeeded.</returns>
	public Result ChangeDisplayName(string displayName, DateTimeOffset changedAt)
	{
		DisplayName = displayName;
		Touch(changedAt);
		return Result.Ok();
	}

	/// <summary>
	/// Stores or clears an external identifier.
	/// </summary>
	/// <param name="externalId">The external identifier.</param>
	/// <param name="changedAt">The mutation timestamp.</param>
	public void SetExternalId(string? externalId, DateTimeOffset changedAt)
	{
		ExternalId = externalId;
		Touch(changedAt);
	}

	/// <summary>
	/// Adds an email to the account.
	/// </summary>
	/// <param name="email">The email entity to attach.</param>
	/// <param name="changedAt">The mutation timestamp.</param>
	/// <returns>A result describing whether the email was added.</returns>
	public Result AddEmail(UserAccountEmail email, DateTimeOffset changedAt)
	{
		if (email.RealmId != RealmId)
		{
			return Problems.InvalidState("Email realm does not match account realm.", nameof(RealmId), "user_account.email_realm_mismatch");
		}

		if (EmailItems.Any(e => e.NormalizedAddress == email.NormalizedAddress))
		{
			return Problems.InvalidState("Email already exists in this account.", nameof(Emails), "user_account.email_duplicate");
		}

		var shouldBePrimary = email.IsPrimary || EmailItems.Count is 0;
		if (shouldBePrimary)
		{
			ClearPrimaryEmail();
		}

		email.AttachTo(this);
		email.MarkPrimary(shouldBePrimary);
		EmailItems.Add(email);
		Touch(changedAt);
		AddEvent(new UserAccountEmailAdded(RealmId, SubjectId, email.Address, email.IsPrimary));

		if (email.IsPrimary)
		{
			AddEvent(new UserAccountPrimaryEmailChanged(RealmId, SubjectId, email.Address));
		}

		return Result.Ok();
	}

	/// <summary>
	/// Changes the primary email.
	/// </summary>
	/// <param name="normalizedAddress">The normalized email address to make primary.</param>
	/// <param name="changedAt">The mutation timestamp.</param>
	/// <returns>A result describing whether the change succeeded.</returns>
	public Result ChangePrimaryEmail(string normalizedAddress, DateTimeOffset changedAt)
	{
		var email = EmailItems.FirstOrDefault(e => e.NormalizedAddress == normalizedAddress);
		if (email is null)
		{
			return Problems.InvalidState("Email does not exist in this account.", nameof(Emails), "user_account.email_missing");
		}

		ClearPrimaryEmail();
		email.MarkPrimary(true);
		Touch(changedAt);
		AddEvent(new UserAccountPrimaryEmailChanged(RealmId, SubjectId, email.Address));
		return Result.Ok();
	}

	/// <summary>
	/// Adds a role directly to the account.
	/// </summary>
	/// <param name="role">The role entity to attach.</param>
	/// <param name="changedAt">The mutation timestamp.</param>
	/// <returns>A result describing whether the role was added.</returns>
	public Result AddRole(UserAccountRole role, DateTimeOffset changedAt)
	{
		if (role.RealmId != RealmId)
		{
			return Problems.InvalidState("Role realm does not match account realm.", nameof(RealmId), "user_account.role_realm_mismatch");
		}

		if (RoleItems.Any(r => r.NormalizedName == role.NormalizedName))
		{
			return Problems.InvalidState("Role already exists in this account.", nameof(Roles), "user_account.role_duplicate");
		}

		role.AttachTo(this);
		RoleItems.Add(role);
		Touch(changedAt);
		AddEvent(new UserAccountRoleAdded(RealmId, SubjectId, role.Name));
		return Result.Ok();
	}

	/// <summary>
	/// Removes a role directly from the account.
	/// </summary>
	/// <param name="normalizedRoleName">The normalized role name.</param>
	/// <param name="changedAt">The mutation timestamp.</param>
	/// <returns>A result describing whether the role was removed.</returns>
	public Result RemoveRole(string normalizedRoleName, DateTimeOffset changedAt)
	{
		var accountRole = RoleItems.FirstOrDefault(r => r.NormalizedName == normalizedRoleName);
		if (accountRole is null)
		{
			return Result.Ok();
		}

		RoleItems.Remove(accountRole);
		Touch(changedAt);
		AddEvent(new UserAccountRoleRemoved(RealmId, SubjectId, accountRole.Name));
		return Result.Ok();
	}

	/// <summary>
	/// Sets or replaces the local password credential.
	/// </summary>
	/// <param name="passwordHash">The new password hash.</param>
	/// <param name="changedAt">The mutation timestamp.</param>
	/// <param name="mustChangePassword">Whether the user must change the password later.</param>
	/// <returns>A result describing whether the password was stored.</returns>
	public Result SetPassword(
		string passwordHash,
		DateTimeOffset changedAt,
		bool mustChangePassword = false)
	{
		LocalCredential.SetPassword(passwordHash, changedAt, mustChangePassword);
		Touch(changedAt);
		AddEvent(new UserAccountPasswordChanged(RealmId, SubjectId));
		return Result.Ok();
	}

	/// <summary>
	/// Changes the local password after verifying the current password.
	/// </summary>
	/// <param name="currentPassword">The current plain password.</param>
	/// <param name="newPasswordHash">The new password hash.</param>
	/// <param name="passwordHasher">The password hasher.</param>
	/// <param name="changedAt">The mutation timestamp.</param>
	/// <returns>A result describing whether the password was changed.</returns>
	public Result ChangePassword(
		string currentPassword,
		string newPasswordHash,
		IUserAccountPasswordHasher passwordHasher,
		DateTimeOffset changedAt)
	{
		if (!LocalCredential.HasPassword)
		{
			return Problems.InvalidState("Account does not have a local password.", nameof(LocalCredential), "user_account.password_not_set");
		}

		if (!passwordHasher.Verify(currentPassword, LocalCredential.PasswordHash!))
		{
			return Problems.InvalidParameter("Current password is invalid.", nameof(LocalCredential), "user_account.current_password_invalid");
		}

		return SetPassword(newPasswordHash, changedAt);
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
		if (!IsActive)
		{
			return LocalAuthenticationResult.Failed(LocalAuthenticationFailureReason.Inactive);
		}

		if (IsBlocked)
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
		if (IsActive)
		{
			return;
		}

		IsActive = true;
		Touch(changedAt);
		AddEvent(new UserAccountActivated(RealmId, SubjectId));
	}

	/// <summary>
	/// Marks the account as inactive.
	/// </summary>
	/// <param name="changedAt">The mutation timestamp.</param>
	public void Deactivate(DateTimeOffset changedAt)
	{
		if (!IsActive)
		{
			return;
		}

		IsActive = false;
		Touch(changedAt);
		AddEvent(new UserAccountDeactivated(RealmId, SubjectId));
	}

	/// <summary>
	/// Blocks the account.
	/// </summary>
	/// <param name="reason">Optional block reason.</param>
	/// <param name="changedAt">The mutation timestamp.</param>
	public void Block(string? reason, DateTimeOffset changedAt)
	{
		BlockState = UserAccountBlockState.Blocked(reason, changedAt);
		Touch(changedAt);
		AddEvent(new UserAccountBlocked(RealmId, SubjectId, reason));
	}

	/// <summary>
	/// Clears the administrative block.
	/// </summary>
	/// <param name="changedAt">The mutation timestamp.</param>
	public void Unblock(DateTimeOffset changedAt)
	{
		if (!IsBlocked)
		{
			return;
		}

		BlockState = UserAccountBlockState.Unblocked();
		Touch(changedAt);
		AddEvent(new UserAccountUnblocked(RealmId, SubjectId));
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

	internal Result ReplacePropertyValues(
		PropertyDefinition definition,
		PropertyValueType valueType,
		IReadOnlyList<string> values,
		DateTimeOffset changedAt)
	{
		if (definition.RealmId != RealmId)
		{
			return Problems.InvalidState(
				"Property definition realm does not match account realm.",
				nameof(RealmId),
				"user_account.property_realm_mismatch");
		}

		if (string.IsNullOrWhiteSpace(definition.ClaimType))
		{
			return Problems.InvalidState(
				"Property definition claim type is missing.",
				nameof(definition),
				"user_account.property_claim_type_missing");
		}

		foreach (var existingValue in PropertyValueItems.Where(v => v.ClaimType == definition.ClaimType).ToArray())
		{
			PropertyValueItems.Remove(existingValue);
		}

		for (var index = 0; index < values.Count; index++)
		{
			PropertyValueItems.Add(new UserAccountPropertyValue(this, definition, valueType, values[index], index));
		}

		Touch(changedAt);
		AddEvent(new UserAccountPropertyValueChanged(RealmId, SubjectId, definition.ClaimType));
		return Result.Ok();
	}

	private void ClearPrimaryEmail()
	{
		foreach (var email in EmailItems.Where(e => e.IsPrimary))
		{
			email.MarkPrimary(false);
		}
	}

	private void Touch(DateTimeOffset changedAt)
	{
		UpdatedAt = changedAt;
	}
}
