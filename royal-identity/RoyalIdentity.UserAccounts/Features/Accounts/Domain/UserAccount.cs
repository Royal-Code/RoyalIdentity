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
	private List<UserAccountPhone> phones = [];
	private List<UserAccountPropertyValue> propertyValues = [];
	private List<UserAccountRole> roles = [];
	private List<PasswordHistoryEntry> passwordHistory = [];

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
		SecurityStamp = SecurityStamp.New();
		SessionsValidAfter = createdAt;
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
	/// Gets the administrative block state. <see cref="IsBlocked"/> reflects whether a block is configured; use
	/// <see cref="IsBlockedAt"/> to evaluate whether it is in effect at a point in time (the block may carry a
	/// scheduled or expired window).
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
	/// Gets the opaque version of the account's security-sensitive state.
	/// </summary>
	public SecurityStamp SecurityStamp { get; private set; } = SecurityStamp.New();

	/// <summary>
	/// Gets the point after which sessions and tokens remain valid when security-state invalidation is enforced.
	/// </summary>
	public DateTimeOffset SessionsValidAfter { get; private set; }

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
	/// Gets account phone numbers.
	/// </summary>
	public IReadOnlyCollection<UserAccountPhone> Phones => phones;

	/// <summary>
	/// Gets account roles.
	/// </summary>
	public IReadOnlyCollection<UserAccountRole> Roles => roles;

	/// <summary>
	/// Gets dynamic property values assigned to this account.
	/// </summary>
	public IReadOnlyCollection<UserAccountPropertyValue> PropertyValues => propertyValues;

	/// <summary>
	/// Gets the archived password hashes kept for reuse enforcement.
	/// </summary>
	public IReadOnlyCollection<PasswordHistoryEntry> PasswordHistory => passwordHistory;

	/// <summary>
	/// Gets the current primary email, when one exists.
	/// </summary>
	public UserAccountEmail? PrimaryEmail => EmailItems.FirstOrDefault(e => e.IsPrimary);

	/// <summary>
	/// Gets the current primary phone, when one exists.
	/// </summary>
	public UserAccountPhone? PrimaryPhone => PhoneItems.FirstOrDefault(p => p.IsPrimary);

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
	protected virtual List<UserAccountPhone> PhoneItems
	{
		get => phones;
		set => phones = value;
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
	/// Collection navigation used by EF Core mapping while public access remains read-only.
	/// </summary>
	protected virtual List<PasswordHistoryEntry> PasswordHistoryItems
	{
		get => passwordHistory;
		set => passwordHistory = value;
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
		TouchSecurityState(changedAt, invalidateSessions: false);
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
		TouchSecurityState(changedAt, invalidateSessions: false);
		AddEvent(new UserAccountPrimaryEmailChanged(RealmId, SubjectId, email.Address));
		return Result.Ok();
	}

	/// <summary>
	/// Marks an account email as verified. Verification targets a specific normalized address: a token issued for
	/// one value never verifies a value replaced later (ADR-017 §2.8). Idempotent when already verified.
	/// </summary>
	/// <param name="normalizedAddress">The normalized email address to verify.</param>
	/// <param name="changedAt">The mutation timestamp.</param>
	/// <returns>A result describing whether the email was verified.</returns>
	public Result VerifyEmail(string normalizedAddress, DateTimeOffset changedAt)
	{
		var email = EmailItems.FirstOrDefault(e => e.NormalizedAddress == normalizedAddress);
		if (email is null)
		{
			return Problems.InvalidState("Email does not exist in this account.", nameof(Emails), "user_account.email_missing");
		}

		if (email.IsVerified)
		{
			return Result.Ok();
		}

		email.MarkVerified();
		TouchSecurityState(changedAt, invalidateSessions: false);
		AddEvent(new UserAccountEmailVerified(RealmId, SubjectId, email.Address));
		return Result.Ok();
	}

	/// <summary>
	/// Adds a phone number to the account.
	/// </summary>
	/// <param name="phone">The phone entity to attach.</param>
	/// <param name="changedAt">The mutation timestamp.</param>
	/// <returns>A result describing whether the phone was added.</returns>
	public Result AddPhone(UserAccountPhone phone, DateTimeOffset changedAt)
	{
		if (phone.RealmId != RealmId)
		{
			return Problems.InvalidState("Phone realm does not match account realm.", nameof(RealmId), "user_account.phone_realm_mismatch");
		}

		if (PhoneItems.Any(p => p.NormalizedNumber == phone.NormalizedNumber))
		{
			return Problems.InvalidState("Phone already exists in this account.", nameof(Phones), "user_account.phone_duplicate");
		}

		var shouldBePrimary = phone.IsPrimary || PhoneItems.Count is 0;
		if (shouldBePrimary)
		{
			ClearPrimaryPhone();
		}

		phone.AttachTo(this);
		phone.MarkPrimary(shouldBePrimary);
		PhoneItems.Add(phone);
		TouchSecurityState(changedAt, invalidateSessions: false);
		AddEvent(new UserAccountPhoneAdded(RealmId, SubjectId, phone.Number, phone.IsPrimary));
		return Result.Ok();
	}

	/// <summary>
	/// Marks an account phone as verified. Like <see cref="VerifyEmail"/>, verification targets a specific
	/// normalized number (ADR-017 §2.8). Idempotent when already verified.
	/// </summary>
	/// <param name="normalizedNumber">The normalized phone number to verify.</param>
	/// <param name="changedAt">The mutation timestamp.</param>
	/// <returns>A result describing whether the phone was verified.</returns>
	public Result VerifyPhone(string normalizedNumber, DateTimeOffset changedAt)
	{
		var phone = PhoneItems.FirstOrDefault(p => p.NormalizedNumber == normalizedNumber);
		if (phone is null)
		{
			return Problems.InvalidState("Phone does not exist in this account.", nameof(Phones), "user_account.phone_missing");
		}

		if (phone.IsVerified)
		{
			return Result.Ok();
		}

		phone.MarkVerified();
		TouchSecurityState(changedAt, invalidateSessions: false);
		AddEvent(new UserAccountPhoneVerified(RealmId, SubjectId, phone.Number));
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
		TouchSecurityState(changedAt, invalidateSessions: false);
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
		TouchSecurityState(changedAt, invalidateSessions: false);
		AddEvent(new UserAccountRoleRemoved(RealmId, SubjectId, accountRole.Name));
		return Result.Ok();
	}

	/// <summary>
	/// Sets or replaces the local password credential. When the realm enforces password history, the previous
	/// hash is archived and the history is pruned to the retained set (quantity ∪ age, bounded by the cap).
	/// </summary>
	/// <param name="passwordHash">The new password hash.</param>
	/// <param name="changedAt">The mutation timestamp.</param>
	/// <param name="options">The realm password and history policy.</param>
	/// <param name="reason">Why the password is being set, recorded on the archived entry.</param>
	/// <param name="changedBySubjectId">The actor who set the password, or <c>null</c> for self-service/system.</param>
	/// <param name="mustChangePassword">Whether the user must change the password later.</param>
	/// <returns>A result describing whether the password was stored.</returns>
	public Result SetPassword(
		string passwordHash,
		DateTimeOffset changedAt,
		PasswordOptions options,
		PasswordChangeReason reason = PasswordChangeReason.Change,
		string? changedBySubjectId = null,
		bool mustChangePassword = false)
	{
		var previousHash = LocalCredential.PasswordHash;

		LocalCredential.SetPassword(passwordHash, changedAt, mustChangePassword);

		if (options.EnforcePasswordHistory && !string.IsNullOrWhiteSpace(previousHash))
		{
			var entry = new PasswordHistoryEntry(RealmId, previousHash!, changedAt, reason, changedBySubjectId);
			entry.AttachTo(this);
			PasswordHistoryItems.Add(entry);
			PrunePasswordHistory(options, changedAt);
		}

		TouchSecurityState(changedAt, invalidateSessions: true);
		AddEvent(new UserAccountPasswordChanged(RealmId, SubjectId));
		return Result.Ok();
	}

	/// <summary>
	/// Regenerates the account security stamp.
	/// </summary>
	/// <param name="changedAt">The mutation timestamp.</param>
	/// <param name="invalidateSessions">Whether this change moves the session invalidation marker.</param>
	public void RegenerateSecurityStamp(DateTimeOffset changedAt, bool invalidateSessions = false)
	{
		TouchSecurityState(changedAt, invalidateSessions);
	}

	/// <summary>
	/// Changes the local password after verifying the current password.
	/// </summary>
	/// <param name="currentPassword">The current plain password.</param>
	/// <param name="newPasswordHash">The new password hash.</param>
	/// <param name="passwordHasher">The password hasher.</param>
	/// <param name="options">The realm password and history policy.</param>
	/// <param name="changedAt">The mutation timestamp.</param>
	/// <returns>A result describing whether the password was changed.</returns>
	public Result ChangePassword(
		string currentPassword,
		string newPasswordHash,
		IUserAccountPasswordHasher passwordHasher,
		PasswordOptions options,
		DateTimeOffset changedAt)
	{
		var verifyResult = VerifyCurrentPassword(currentPassword, passwordHasher);
		if (verifyResult.IsFailure)
		{
			return verifyResult;
		}

		return SetPassword(newPasswordHash, changedAt, options, PasswordChangeReason.Change);
	}

	/// <summary>
	/// Verifies the current local password without mutating the account.
	/// </summary>
	/// <param name="currentPassword">The current plain password.</param>
	/// <param name="passwordHasher">The password hasher.</param>
	/// <returns>A result describing whether the current password is valid for a password change.</returns>
	public Result VerifyCurrentPassword(string currentPassword, IUserAccountPasswordHasher passwordHasher)
	{
		if (!LocalCredential.HasPassword)
		{
			return Problems.InvalidState("Account does not have a local password.", nameof(LocalCredential), "user_account.password_not_set");
		}

		if (!passwordHasher.Verify(currentPassword, LocalCredential.PasswordHash!))
		{
			return Problems.InvalidParameter("Current password is invalid.", nameof(LocalCredential), "user_account.current_password_invalid");
		}

		return Result.Ok();
	}

	/// <summary>
	/// Resets the local password from a recovery flow. Unlike <see cref="ChangePassword"/> this does not verify a
	/// current password — the recovery token already proved control of the account. The reset clears any forced
	/// change/lockout (via <see cref="SetPassword"/>), archives the previous hash and moves both the
	/// <see cref="SecurityStamp"/> and <see cref="SessionsValidAfter"/> (a reset is a credential-compromise trigger,
	/// ADR-017 §2.6/§2.7). The active revocation of existing sessions/refresh tokens is executed at the edge (Fase 8).
	/// </summary>
	/// <param name="newPasswordHash">The new password hash.</param>
	/// <param name="options">The realm password and history policy.</param>
	/// <param name="changedAt">The mutation timestamp.</param>
	/// <returns>A result describing whether the password was reset.</returns>
	public Result ResetPassword(string newPasswordHash, PasswordOptions options, DateTimeOffset changedAt)
	{
		return SetPassword(newPasswordHash, changedAt, options, PasswordChangeReason.Reset);
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

		if (IsBlockedAt(attemptedAt))
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

		// Credential is valid, but completion may be gated by a required action (ADR-017 §2.3): an admin-forced
		// change takes precedence over a policy expiration. No session/token is issued by the caller until the
		// action is satisfied; the reset of failed-attempt state above is still persisted.
		if (LocalCredential.MustChangePassword)
		{
			return LocalAuthenticationResult.RequiresAction(this, LocalRequiredAction.ChangePasswordMustChange);
		}

		if (LocalCredential.IsPasswordExpired(options, attemptedAt))
		{
			return LocalAuthenticationResult.RequiresAction(this, LocalRequiredAction.ChangePasswordExpired);
		}

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
	/// Blocks the account, optionally scheduled to a time window (ADR-017 §2.5). With both bounds <c>null</c> the
	/// block is effective immediately and indefinitely. The window is enforced at authentication time via
	/// <see cref="IsBlockedAt"/>; window validity is checked by the feature that issues the block.
	/// </summary>
	/// <param name="reason">Optional block reason.</param>
	/// <param name="changedAt">The mutation timestamp.</param>
	/// <param name="startsAt">When the block becomes effective, or <c>null</c> for immediately.</param>
	/// <param name="endsAt">When the block expires, or <c>null</c> for indefinite.</param>
	public void Block(
		string? reason,
		DateTimeOffset changedAt,
		DateTimeOffset? startsAt = null,
		DateTimeOffset? endsAt = null)
	{
		BlockState = UserAccountBlockState.Blocked(reason, changedAt, startsAt, endsAt);
		Touch(changedAt);
		AddEvent(new UserAccountBlocked(RealmId, SubjectId, reason, startsAt, endsAt));
	}

	/// <summary>
	/// Gets whether the account is administratively blocked in effect at <paramref name="now"/>, honoring the
	/// optional block window. This is distinct from credential lockout (failed-attempt throttling).
	/// </summary>
	/// <param name="now">The instant to evaluate.</param>
	/// <returns><c>true</c> when an administrative block is in effect.</returns>
	public bool IsBlockedAt(DateTimeOffset now)
	{
		return BlockState.IsActiveAt(now);
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
	/// Clears local credential lockout counters (administrative unlock — ADR-017 §2.5). Resets the failed-attempt
	/// counter and any active lockout, including an indefinite lockout (when the realm sets no lockout duration),
	/// and bumps the version. Raises <see cref="UserAccountLocalCredentialUnlocked"/> only when there was lockout
	/// state to clear, so a no-op unlock stays silent.
	/// </summary>
	/// <param name="changedAt">The mutation timestamp.</param>
	public void UnlockLocalCredential(DateTimeOffset changedAt)
	{
		var hadLockoutState = LocalCredential.FailedPasswordAttempts > 0 || LocalCredential.LockoutEndAt is not null;

		LocalCredential.ResetFailures();
		Touch(changedAt);

		if (hadLockoutState)
		{
			AddEvent(new UserAccountLocalCredentialUnlocked(RealmId, SubjectId));
		}
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

	private void ClearPrimaryPhone()
	{
		foreach (var phone in PhoneItems.Where(p => p.IsPrimary))
		{
			phone.MarkPrimary(false);
		}
	}

	private void Touch(DateTimeOffset changedAt)
	{
		UpdatedAt = changedAt;
		Version++;
	}

	private void TouchSecurityState(DateTimeOffset changedAt, bool invalidateSessions)
	{
		SecurityStamp = SecurityStamp.New();

		if (invalidateSessions)
		{
			SessionsValidAfter = changedAt;
		}

		Touch(changedAt);
	}

	/// <summary>
	/// Keeps the password history entries that are still relevant (within the retained quantity or the reuse age
	/// window) and drops the rest, never retaining more than the comparison/retention cap.
	/// </summary>
	private void PrunePasswordHistory(PasswordOptions options, DateTimeOffset now)
	{
		var ordered = PasswordHistoryItems
			.OrderByDescending(h => h.CreatedAt)
			.ThenByDescending(h => h.Id)
			.ToList();

		DateTimeOffset? windowStart = options.PasswordReuseWindowDays > 0
			? now - TimeSpan.FromDays(options.PasswordReuseWindowDays)
			: null;

		var retained = new List<PasswordHistoryEntry>(ordered.Count);
		for (var index = 0; index < ordered.Count; index++)
		{
			if (retained.Count >= options.MaxPasswordHistoryComparisons)
			{
				break;
			}

			var entry = ordered[index];
			var withinCount = index < options.PasswordHistoryCount;
			var withinAge = windowStart is not null && entry.CreatedAt >= windowStart;
			if (withinCount || withinAge)
			{
				retained.Add(entry);
			}
		}

		foreach (var entry in ordered.Where(e => !retained.Contains(e)))
		{
			PasswordHistoryItems.Remove(entry);
		}
	}
}
