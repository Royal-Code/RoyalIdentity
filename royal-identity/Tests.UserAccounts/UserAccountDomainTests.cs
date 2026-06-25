using RoyalIdentity.UserAccounts.Features.Accounts.Domain;
using RoyalIdentity.UserAccounts.Options;
using System.Reflection;

namespace Tests.UserAccounts;

public class UserAccountDomainTests
{
	private static readonly DateTimeOffset Now = new(2026, 6, 18, 10, 0, 0, TimeSpan.Zero);
	private static readonly TestPasswordHasher PasswordHasher = new();

	[Fact]
	public void Constructor_StoresValidatedIdentity_AndInitializesCredential()
	{
		var account = CreateAccount();

		Assert.Equal("realm-a", account.RealmId);
		Assert.Equal("subject-1", account.SubjectId);
		Assert.Equal("alice", account.Username);
		Assert.Equal("ALICE", account.NormalizedUsername);
		Assert.Equal("Alice", account.DisplayName);
		Assert.True(account.IsActive);
		Assert.False(account.IsBlocked);
		Assert.False(account.BlockState.IsBlocked);
		Assert.NotEmpty(account.SecurityStamp.Value);
		Assert.Equal(Now, account.SessionsValidAfter);
		Assert.Equal("realm-a", account.LocalCredential.RealmId);
		Assert.Same(account, account.LocalCredential.UserAccount);
	}

	[Fact]
	public void ChangeUsername_DoesNotChangeSubjectId()
	{
		var account = CreateAccount();
		var subject = account.SubjectId;

		var result = account.ChangeUsername("alice.renamed", "ALICE.RENAMED", Now.AddMinutes(1));

		Assert.True(result.IsSuccess);
		Assert.Equal(subject, account.SubjectId);
		Assert.Equal("alice.renamed", account.Username);
		Assert.Equal("ALICE.RENAMED", account.NormalizedUsername);
	}

	[Fact]
	public void AddEmail_MaintainsSinglePrimary_AndStoresVerificationAndFictitiousFlags()
	{
		var account = CreateAccount();
		var verifiedEmail = CreateEmail("alice@example.com", isPrimary: true, isVerified: true);
		var fictitiousEmail = CreateEmail("subject-1@fictitious.local", isPrimary: true, isFictitious: true);

		Assert.True(account.AddEmail(verifiedEmail, Now).IsSuccess);
		Assert.True(account.AddEmail(fictitiousEmail, Now.AddMinutes(1)).IsSuccess);

		Assert.Single(account.Emails, e => e.IsPrimary);
		Assert.Equal("subject-1@fictitious.local", account.PrimaryEmail?.Address);
		Assert.Contains(account.Emails, e => e.Address == "alice@example.com" && e.IsVerified && !e.IsPrimary);
		Assert.Contains(account.Emails, e => e.Address == "subject-1@fictitious.local" && e.IsFictitious && e.IsPrimary);
		Assert.All(account.Emails, email => Assert.Same(account, email.UserAccount));
	}

	[Fact]
	public void AddEmail_RejectsRealmMismatch_AndDuplicateNormalizedAddress()
	{
		var account = CreateAccount();

		Assert.True(account.AddEmail(CreateEmail("alice@example.com"), Now).IsSuccess);

		var duplicate = account.AddEmail(CreateEmail("Alice@example.com"), Now.AddMinutes(1));
		var realmMismatch = account.AddEmail(
			new UserAccountEmail("realm-b", "other@example.com", "OTHER@EXAMPLE.COM", false, false, false),
			Now.AddMinutes(2));

		Assert.True(duplicate.IsFailure);
		Assert.True(realmMismatch.IsFailure);
	}

	[Fact]
	public void AddRole_TreatsRolesAsFirstClassValues_AndRejectsDuplicates()
	{
		var account = CreateAccount();

		Assert.True(account.AddRole(CreateRole("admin"), Now).IsSuccess);
		Assert.True(account.AddRole(CreateRole("Admin"), Now.AddMinutes(1)).IsFailure);

		var role = Assert.Single(account.Roles);
		Assert.Equal("admin", role.Name);
		Assert.Equal("ADMIN", role.NormalizedName);
		Assert.Same(account, role.UserAccount);
	}

	[Fact]
	public void PasswordPolicy_RejectsBasicComplexityViolations()
	{
		var account = CreateAccount();
		var options = RelaxedPasswordOptions();
		options.MinimumLength = 10;
		options.RequireDigit = true;
		var policy = new PasswordPolicy();

		var tooShort = policy.Validate("Aa1!", options, account.Username);
		var withoutDigit = policy.Validate("Valid-pass", options, account.Username);
		var valid = policy.Validate("Valid-pass1", options, account.Username);

		Assert.True(tooShort.IsFailure);
		Assert.True(withoutDigit.IsFailure);
		Assert.True(valid.IsSuccess);
		Assert.True(account.SetPassword(PasswordHasher.Hash("Valid-pass1"), Now, RelaxedPasswordOptions()).IsSuccess);
		Assert.True(account.LocalCredential.HasPassword);
	}

	[Fact]
	public void SetPassword_RegeneratesSecurityStamp_AndMovesSessionsValidAfter()
	{
		var account = CreateAccount();
		var initialStamp = account.SecurityStamp;
		var changedAt = Now.AddMinutes(1);

		Assert.True(account.SetPassword(PasswordHasher.Hash("Valid-pass1"), changedAt, RelaxedPasswordOptions()).IsSuccess);

		Assert.NotEqual(initialStamp, account.SecurityStamp);
		Assert.Equal(changedAt, account.SessionsValidAfter);
		Assert.Equal(changedAt, account.UpdatedAt);
	}

	[Fact]
	public void SensitiveNonCredentialChanges_RegenerateSecurityStamp_WithoutMovingSessionsValidAfter()
	{
		var account = CreateAccount();
		var initialStamp = account.SecurityStamp;
		var initialValidAfter = account.SessionsValidAfter;

		Assert.True(account.AddEmail(CreateEmail("alice@example.com"), Now.AddMinutes(1)).IsSuccess);
		var emailStamp = account.SecurityStamp;

		Assert.NotEqual(initialStamp, emailStamp);
		Assert.Equal(initialValidAfter, account.SessionsValidAfter);

		Assert.True(account.AddRole(CreateRole("admin"), Now.AddMinutes(2)).IsSuccess);

		Assert.NotEqual(emailStamp, account.SecurityStamp);
		Assert.Equal(initialValidAfter, account.SessionsValidAfter);
	}

	[Fact]
	public void ProfileChanges_DoNotRegenerateSecurityStamp()
	{
		var account = CreateAccount();
		var initialStamp = account.SecurityStamp;
		var initialValidAfter = account.SessionsValidAfter;

		Assert.True(account.ChangeDisplayName("Alice L.", Now.AddMinutes(1)).IsSuccess);
		Assert.True(account.ChangeUsername("alice.l", "ALICE.L", Now.AddMinutes(2)).IsSuccess);

		Assert.Equal(initialStamp, account.SecurityStamp);
		Assert.Equal(initialValidAfter, account.SessionsValidAfter);
	}

	[Fact]
	public void AuthenticateLocal_DoesNotRegenerateSecurityStamp_OrMoveSessionsValidAfter()
	{
		var account = CreateAccount();
		var options = RelaxedPasswordOptions();
		Assert.True(account.SetPassword(PasswordHasher.Hash("Valid-pass1"), Now, RelaxedPasswordOptions()).IsSuccess);
		var stampAfterPassword = account.SecurityStamp;
		var validAfterPassword = account.SessionsValidAfter;

		var failed = account.AuthenticateLocal("wrong", options, PasswordHasher, Now.AddMinutes(1));
		var succeeded = account.AuthenticateLocal("Valid-pass1", options, PasswordHasher, Now.AddMinutes(2));

		Assert.False(failed.Success);
		Assert.True(succeeded.Success);
		Assert.Equal(stampAfterPassword, account.SecurityStamp);
		Assert.Equal(validAfterPassword, account.SessionsValidAfter);
	}

	[Fact]
	public void RegenerateSecurityStamp_CanOptionallyMoveSessionsValidAfter()
	{
		var account = CreateAccount();
		var initialStamp = account.SecurityStamp;
		var initialValidAfter = account.SessionsValidAfter;

		account.RegenerateSecurityStamp(Now.AddMinutes(1));
		var regeneratedStamp = account.SecurityStamp;

		Assert.NotEqual(initialStamp, regeneratedStamp);
		Assert.Equal(initialValidAfter, account.SessionsValidAfter);

		account.RegenerateSecurityStamp(Now.AddMinutes(2), invalidateSessions: true);

		Assert.NotEqual(regeneratedStamp, account.SecurityStamp);
		Assert.Equal(Now.AddMinutes(2), account.SessionsValidAfter);
	}

	[Fact]
	public void AuthenticateLocal_ReturnsPasswordNotSet_WhenNoLocalCredentialExists()
	{
		var account = CreateAccount();

		var result = account.AuthenticateLocal("any-password", RelaxedPasswordOptions(), PasswordHasher, Now);

		Assert.False(result.Success);
		Assert.Equal(LocalAuthenticationFailureReason.PasswordNotSet, result.Reason);
	}

	[Fact]
	public void AuthenticateLocal_IncrementsResetsAndExpiresLockout()
	{
		var account = CreateAccount();
		var options = RelaxedPasswordOptions();
		options.MaxFailedAccessAttempts = 2;
		options.AccountLockoutDurationMinutes = 10;
		Assert.True(account.SetPassword(PasswordHasher.Hash("Valid-pass1"), Now, RelaxedPasswordOptions()).IsSuccess);

		var firstFailure = account.AuthenticateLocal("wrong", options, PasswordHasher, Now.AddMinutes(1));

		Assert.False(firstFailure.Success);
		Assert.Equal(LocalAuthenticationFailureReason.InvalidCredentials, firstFailure.Reason);
		Assert.Equal(1, account.LocalCredential.FailedPasswordAttempts);

		var secondFailure = account.AuthenticateLocal("wrong", options, PasswordHasher, Now.AddMinutes(2));

		Assert.False(secondFailure.Success);
		Assert.Equal(LocalAuthenticationFailureReason.InvalidCredentials, secondFailure.Reason);
		Assert.Equal(2, account.LocalCredential.FailedPasswordAttempts);

		var locked = account.AuthenticateLocal("Valid-pass1", options, PasswordHasher, Now.AddMinutes(3));

		Assert.False(locked.Success);
		Assert.Equal(LocalAuthenticationFailureReason.LockedOut, locked.Reason);
		Assert.Equal(Now.AddMinutes(12), locked.LockoutEndAt);

		var successAfterExpiration = account.AuthenticateLocal("Valid-pass1", options, PasswordHasher, Now.AddMinutes(13));

		Assert.True(successAfterExpiration.Success);
		Assert.Equal("subject-1", successAfterExpiration.SubjectId);
		Assert.Equal(0, account.LocalCredential.FailedPasswordAttempts);
		Assert.Null(account.LocalCredential.LockoutEndAt);
	}

	[Fact]
	public void AuthenticateLocal_MapsActivationAndBlockFailures()
	{
		var inactive = CreateAccount();
		var blocked = CreateAccount();
		var options = RelaxedPasswordOptions();
		Assert.True(inactive.SetPassword(PasswordHasher.Hash("Valid-pass1"), Now, RelaxedPasswordOptions()).IsSuccess);
		Assert.True(blocked.SetPassword(PasswordHasher.Hash("Valid-pass1"), Now, RelaxedPasswordOptions()).IsSuccess);

		inactive.Deactivate(Now.AddMinutes(1));
		blocked.Block("administrative", Now.AddMinutes(1));

		Assert.True(blocked.IsBlocked);
		Assert.True(blocked.BlockState.IsBlocked);
		Assert.Equal("administrative", blocked.BlockedReason);
		Assert.Equal(Now.AddMinutes(1), blocked.BlockedAt);

		var inactiveResult = inactive.AuthenticateLocal("Valid-pass1", options, PasswordHasher, Now.AddMinutes(2));
		var blockedResult = blocked.AuthenticateLocal("Valid-pass1", options, PasswordHasher, Now.AddMinutes(2));

		Assert.False(inactiveResult.Success);
		Assert.Equal(LocalAuthenticationFailureReason.Inactive, inactiveResult.Reason);
		Assert.False(blockedResult.Success);
		Assert.Equal(LocalAuthenticationFailureReason.Blocked, blockedResult.Reason);
	}

	[Fact]
	public void AuthenticateLocal_RequiresPasswordChange_WhenMustChangePasswordIsSet()
	{
		var account = CreateAccount();
		var options = RelaxedPasswordOptions();
		Assert.True(account.SetPassword(PasswordHasher.Hash("Valid-pass1"), Now, options, mustChangePassword: true).IsSuccess);

		var result = account.AuthenticateLocal("Valid-pass1", options, PasswordHasher, Now.AddMinutes(1));

		// Valid credential, but completion is gated: not a success, no failure reason, subject carried.
		Assert.False(result.Success);
		Assert.Null(result.Reason);
		Assert.Equal(LocalRequiredAction.ChangePasswordMustChange, result.RequiredAction);
		Assert.Equal("subject-1", result.SubjectId);
	}

	[Fact]
	public void AuthenticateLocal_RequiresPasswordChange_WhenPasswordExpired()
	{
		var account = CreateAccount();
		var options = RelaxedPasswordOptions();
		options.EnablePasswordExpiration = true;
		options.PasswordExpirationDays = 10;
		Assert.True(account.SetPassword(PasswordHasher.Hash("Valid-pass1"), Now, options).IsSuccess);

		var withinWindow = account.AuthenticateLocal("Valid-pass1", options, PasswordHasher, Now.AddDays(9));
		var afterExpiration = account.AuthenticateLocal("Valid-pass1", options, PasswordHasher, Now.AddDays(11));

		Assert.True(withinWindow.Success);
		Assert.Null(withinWindow.RequiredAction);

		Assert.False(afterExpiration.Success);
		Assert.Null(afterExpiration.Reason);
		Assert.Equal(LocalRequiredAction.ChangePasswordExpired, afterExpiration.RequiredAction);
		Assert.Equal("subject-1", afterExpiration.SubjectId);
	}

	[Fact]
	public void AuthenticateLocal_DoesNotSurfaceRequiredAction_OnInvalidPassword()
	{
		var account = CreateAccount();
		var options = RelaxedPasswordOptions();
		Assert.True(account.SetPassword(PasswordHasher.Hash("Valid-pass1"), Now, options, mustChangePassword: true).IsSuccess);

		var result = account.AuthenticateLocal("wrong", options, PasswordHasher, Now.AddMinutes(1));

		// A wrong password is a plain failure; the required action only gates an otherwise valid credential.
		Assert.False(result.Success);
		Assert.Null(result.RequiredAction);
		Assert.Equal(LocalAuthenticationFailureReason.InvalidCredentials, result.Reason);
	}

	[Fact]
	public void AuthenticateLocal_ResetsFailures_WhenValidButRequiresAction()
	{
		var account = CreateAccount();
		var options = RelaxedPasswordOptions();
		options.MaxFailedAccessAttempts = 5;
		Assert.True(account.SetPassword(PasswordHasher.Hash("Valid-pass1"), Now, options, mustChangePassword: true).IsSuccess);

		account.AuthenticateLocal("wrong", options, PasswordHasher, Now.AddMinutes(1));
		Assert.Equal(1, account.LocalCredential.FailedPasswordAttempts);

		var required = account.AuthenticateLocal("Valid-pass1", options, PasswordHasher, Now.AddMinutes(2));

		Assert.False(required.Success);
		Assert.Equal(LocalRequiredAction.ChangePasswordMustChange, required.RequiredAction);
		Assert.Equal(0, account.LocalCredential.FailedPasswordAttempts);
	}

	[Fact]
	public void ResetPassword_ClearsForcedChangeAndLockout_AndMovesSessionsValidAfter()
	{
		var account = CreateAccount();
		var options = RelaxedPasswordOptions();
		options.MaxFailedAccessAttempts = 2;
		Assert.True(account.SetPassword(PasswordHasher.Hash("old-pass"), Now, options, mustChangePassword: true).IsSuccess);

		// A failed attempt before the reset leaves failure state that the reset must clear.
		account.AuthenticateLocal("wrong", options, PasswordHasher, Now.AddMinutes(1));
		Assert.Equal(1, account.LocalCredential.FailedPasswordAttempts);
		var stampBefore = account.SecurityStamp;
		var resetAt = Now.AddMinutes(2);

		Assert.True(account.ResetPassword(PasswordHasher.Hash("new-pass"), options, resetAt).IsSuccess);

		Assert.False(account.LocalCredential.MustChangePassword);
		Assert.Equal(0, account.LocalCredential.FailedPasswordAttempts);
		Assert.NotEqual(stampBefore, account.SecurityStamp);
		Assert.Equal(resetAt, account.SessionsValidAfter);

		// The new password authenticates without the required action.
		var auth = account.AuthenticateLocal("new-pass", options, PasswordHasher, Now.AddMinutes(3));
		Assert.True(auth.Success);
		Assert.Null(auth.RequiredAction);
	}

	[Fact]
	public void ActionToken_IsConsumable_RespectsExpiryRevocation_AndRevokeIsIdempotent()
	{
		var account = CreateAccount();
		var expiresAt = Now.AddMinutes(30);
		var token = UserAccountActionToken.Issue(
			account, ActionTokenPurpose.PasswordRecovery, "token-hash", "alice@example.com", Now, expiresAt);

		Assert.True(token.IsConsumable(Now.AddMinutes(10)));
		Assert.False(token.IsConsumable(Now.AddMinutes(31)));

		token.Revoke(ActionTokenRevocationReason.Superseded, Now.AddMinutes(5));
		Assert.False(token.IsConsumable(Now.AddMinutes(10)));
		Assert.Equal(Now.AddMinutes(5), token.RevokedAt);
		Assert.Equal(ActionTokenRevocationReason.Superseded, token.RevokedReason);

		// Revoking again does not move the recorded revocation.
		token.Revoke(ActionTokenRevocationReason.Superseded, Now.AddMinutes(9));
		Assert.Equal(Now.AddMinutes(5), token.RevokedAt);
	}

	[Fact]
	public void VerifyEmail_MarksTargetVerified_IsIdempotent_AndFailsForMissing()
	{
		var account = CreateAccount();
		Assert.True(account.AddEmail(CreateEmail("a@example.com", isPrimary: true), Now).IsSuccess);
		Assert.True(account.AddEmail(CreateEmail("b@example.com"), Now.AddMinutes(1)).IsSuccess);

		Assert.True(account.VerifyEmail("A@EXAMPLE.COM", Now.AddMinutes(2)).IsSuccess);

		// Only the target is verified — a token issued for one value never verifies another.
		Assert.True(account.Emails.Single(e => e.NormalizedAddress == "A@EXAMPLE.COM").IsVerified);
		Assert.False(account.Emails.Single(e => e.NormalizedAddress == "B@EXAMPLE.COM").IsVerified);

		// Idempotent and fails for an unknown target.
		Assert.True(account.VerifyEmail("A@EXAMPLE.COM", Now.AddMinutes(3)).IsSuccess);
		Assert.True(account.VerifyEmail("Z@EXAMPLE.COM", Now.AddMinutes(4)).IsFailure);
	}

	[Fact]
	public void AddPhone_MaintainsSinglePrimary_AndRejectsDuplicatesAndRealmMismatch()
	{
		var account = CreateAccount();

		Assert.True(account.AddPhone(CreatePhone("+5511999990000", isPrimary: true), Now).IsSuccess);
		Assert.True(account.AddPhone(CreatePhone("+5511888880000"), Now.AddMinutes(1)).IsSuccess);

		Assert.Single(account.Phones, p => p.IsPrimary);
		Assert.Equal("+5511999990000", account.PrimaryPhone?.Number);

		var duplicate = account.AddPhone(CreatePhone("+5511999990000"), Now.AddMinutes(2));
		var realmMismatch = account.AddPhone(
			new UserAccountPhone("realm-b", "+100", "+100", false, false), Now.AddMinutes(3));

		Assert.True(duplicate.IsFailure);
		Assert.True(realmMismatch.IsFailure);
	}

	[Fact]
	public void VerifyPhone_MarksTargetVerified_AndMovesStampWithoutInvalidatingSessions()
	{
		var account = CreateAccount();
		Assert.True(account.AddPhone(CreatePhone("+5511999990000", isPrimary: true), Now).IsSuccess);
		var stampBefore = account.SecurityStamp;
		var validAfterBefore = account.SessionsValidAfter;

		Assert.True(account.VerifyPhone("+5511999990000", Now.AddMinutes(1)).IsSuccess);

		Assert.True(account.PrimaryPhone!.IsVerified);
		Assert.NotEqual(stampBefore, account.SecurityStamp);
		Assert.Equal(validAfterBefore, account.SessionsValidAfter);
	}

	[Fact]
	public void BlockAndUnblock_KeepAdministrativeBlockStateTogether()
	{
		var account = CreateAccount();

		account.Block("administrative", Now.AddMinutes(1));

		Assert.True(account.IsBlocked);
		Assert.True(account.BlockState.IsBlocked);
		Assert.Equal("administrative", account.BlockState.BlockedReason);
		Assert.Equal(Now.AddMinutes(1), account.BlockState.BlockedAt);
		Assert.Equal("administrative", account.BlockedReason);
		Assert.Equal(Now.AddMinutes(1), account.BlockedAt);

		account.Unblock(Now.AddMinutes(2));

		Assert.False(account.IsBlocked);
		Assert.False(account.BlockState.IsBlocked);
		Assert.Null(account.BlockState.BlockedReason);
		Assert.Null(account.BlockState.BlockedAt);
		Assert.Null(account.BlockedReason);
		Assert.Null(account.BlockedAt);
	}

	[Fact]
	public void Block_WithWindow_IsEffectiveOnlyWithinWindow_ButStaysConfigured()
	{
		var account = CreateAccount();
		var startsAt = Now.AddDays(2);
		var endsAt = Now.AddDays(9);

		account.Block("vacation", Now, startsAt, endsAt);

		// The block is configured for the whole time, but only in effect inside [startsAt, endsAt).
		Assert.True(account.IsBlocked);
		Assert.Equal(startsAt, account.BlockState.StartsAt);
		Assert.Equal(endsAt, account.BlockState.EndsAt);
		Assert.False(account.IsBlockedAt(Now.AddDays(1)));
		Assert.True(account.IsBlockedAt(startsAt));
		Assert.True(account.IsBlockedAt(Now.AddDays(5)));
		Assert.False(account.IsBlockedAt(endsAt));
		Assert.False(account.IsBlockedAt(Now.AddDays(10)));
	}

	[Fact]
	public void Block_WithoutWindow_IsEffectiveImmediatelyAndIndefinitely()
	{
		var account = CreateAccount();

		account.Block("administrative", Now);

		Assert.True(account.IsBlockedAt(Now));
		Assert.True(account.IsBlockedAt(Now.AddYears(5)));
	}

	[Fact]
	public void AuthenticateLocal_HonorsBlockWindow()
	{
		var account = CreateAccount();
		var options = RelaxedPasswordOptions();
		Assert.True(account.SetPassword(PasswordHasher.Hash("Valid-pass1"), Now, options).IsSuccess);

		account.Block("vacation", Now, Now.AddDays(2), Now.AddDays(9));

		// Before the window the credential authenticates; inside it is blocked; after it authenticates again.
		Assert.True(account.AuthenticateLocal("Valid-pass1", options, PasswordHasher, Now.AddDays(1)).Success);
		Assert.Equal(
			LocalAuthenticationFailureReason.Blocked,
			account.AuthenticateLocal("Valid-pass1", options, PasswordHasher, Now.AddDays(5)).Reason);
		Assert.True(account.AuthenticateLocal("Valid-pass1", options, PasswordHasher, Now.AddDays(10)).Success);
	}

	[Fact]
	public void UnlockLocalCredential_ClearsIndefiniteLockout_AndRaisesEventOnlyWhenLocked()
	{
		var account = CreateAccount();
		var options = RelaxedPasswordOptions();
		options.MaxFailedAccessAttempts = 1;
		options.AccountLockoutDurationMinutes = 0; // indefinite lockout: only an admin unlock clears it
		Assert.True(account.SetPassword(PasswordHasher.Hash("Valid-pass1"), Now, options).IsSuccess);

		// One failure trips the indefinite lockout.
		account.AuthenticateLocal("wrong", options, PasswordHasher, Now.AddMinutes(1));
		Assert.True(account.LocalCredential.IsLockedOut(options, Now.AddMinutes(2)));

		account.UnlockLocalCredential(Now.AddMinutes(3));

		Assert.Equal(0, account.LocalCredential.FailedPasswordAttempts);
		Assert.Null(account.LocalCredential.LockoutEndAt);
		Assert.False(account.LocalCredential.IsLockedOut(options, Now.AddMinutes(4)));
		Assert.Single(account.DomainEvents.OfType<UserAccountLocalCredentialUnlocked>());

		// A second unlock with nothing to clear stays silent.
		account.UnlockLocalCredential(Now.AddMinutes(5));
		Assert.Single(account.DomainEvents.OfType<UserAccountLocalCredentialUnlocked>());
	}

	[Fact]
	public void EntitySurface_IsEfFriendly_AndKeepsCollectionsProtected()
	{
		AssertProtectedParameterlessConstructor(typeof(UserAccount));
		AssertProtectedParameterlessConstructor(typeof(UserAccountBlockState));
		AssertProtectedParameterlessConstructor(typeof(UserAccountEmail));
		AssertProtectedParameterlessConstructor(typeof(UserAccountRole));
		AssertProtectedParameterlessConstructor(typeof(UserAccountCredential));

		Assert.Equal(typeof(IReadOnlyCollection<UserAccountEmail>), typeof(UserAccount).GetProperty(nameof(UserAccount.Emails))?.PropertyType);
		Assert.Equal(typeof(IReadOnlyCollection<UserAccountRole>), typeof(UserAccount).GetProperty(nameof(UserAccount.Roles))?.PropertyType);
		Assert.Equal(typeof(SecurityStamp), typeof(UserAccount).GetProperty(nameof(UserAccount.SecurityStamp))?.PropertyType);
		AssertVirtualGetter(typeof(UserAccount), nameof(UserAccount.LocalCredential));
		AssertVirtualGetter(typeof(UserAccountEmail), nameof(UserAccountEmail.UserAccount));
		AssertVirtualGetter(typeof(UserAccountRole), nameof(UserAccountRole.UserAccount));
		AssertVirtualGetter(typeof(UserAccountCredential), nameof(UserAccountCredential.UserAccount));
		AssertProtectedVirtualNavigation(typeof(UserAccount), "EmailItems");
		AssertProtectedVirtualNavigation(typeof(UserAccount), "RoleItems");
	}

	private static UserAccount CreateAccount()
	{
		return new UserAccount(
			"realm-a",
			"subject-1",
			"alice",
			"ALICE",
			"Alice",
			Now);
	}

	private static UserAccountEmail CreateEmail(
		string address,
		bool isPrimary = false,
		bool isVerified = false,
		bool isFictitious = false)
	{
		return new UserAccountEmail(
			"realm-a",
			address,
			address.ToUpperInvariant(),
			isPrimary,
			isVerified,
			isFictitious);
	}

	private static UserAccountRole CreateRole(string name)
	{
		return new UserAccountRole("realm-a", name, name.ToUpperInvariant());
	}

	private static UserAccountPhone CreatePhone(string number, bool isPrimary = false)
	{
		// The domain compares by normalized number; the use case is the single normalization home.
		return new UserAccountPhone("realm-a", number, number, isPrimary, false);
	}

	private static PasswordOptions RelaxedPasswordOptions()
	{
		return new PasswordOptions
		{
			MinimumLength = 6,
			MaximumLength = 100,
			RequireSpecialCharacters = false,
			RequireDigit = false,
			RequireLowercase = false,
			RequireUppercase = false,
			MinimumUniqueCharacters = 0,
			DisallowUsernameInPassword = false,
			DisallowBirthdateInPassword = false,
			DisallowedWordsInPassword = []
		};
	}

	private static void AssertProtectedParameterlessConstructor(Type type)
	{
		var constructor = type.GetConstructor(
			BindingFlags.Instance | BindingFlags.NonPublic,
			binder: null,
			Type.EmptyTypes,
			modifiers: null);

		Assert.NotNull(constructor);
		Assert.True(constructor.IsFamily);
	}

	private static void AssertVirtualGetter(Type type, string propertyName)
	{
		var property = type.GetProperty(propertyName);

		Assert.NotNull(property?.GetMethod);
		Assert.True(property.GetMethod.IsVirtual);
	}

	private static void AssertProtectedVirtualNavigation(Type type, string propertyName)
	{
		var property = type.GetProperty(propertyName, BindingFlags.Instance | BindingFlags.NonPublic);

		Assert.NotNull(property?.GetMethod);
		Assert.True(property.GetMethod.IsFamily);
		Assert.True(property.GetMethod.IsVirtual);
	}

	private sealed class TestPasswordHasher : IUserAccountPasswordHasher
	{
		public string Hash(string password)
		{
			return $"hashed:{password}";
		}

		public bool Verify(string password, string passwordHash)
		{
			return passwordHash == Hash(password);
		}
	}
}
