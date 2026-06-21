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
		Assert.True(account.SetPassword(PasswordHasher.Hash("Valid-pass1"), Now).IsSuccess);
		Assert.True(account.LocalCredential.HasPassword);
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
		Assert.True(account.SetPassword(PasswordHasher.Hash("Valid-pass1"), Now).IsSuccess);

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
		Assert.True(inactive.SetPassword(PasswordHasher.Hash("Valid-pass1"), Now).IsSuccess);
		Assert.True(blocked.SetPassword(PasswordHasher.Hash("Valid-pass1"), Now).IsSuccess);

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
	public void EntitySurface_IsEfFriendly_AndKeepsCollectionsProtected()
	{
		AssertProtectedParameterlessConstructor(typeof(UserAccount));
		AssertProtectedParameterlessConstructor(typeof(UserAccountBlockState));
		AssertProtectedParameterlessConstructor(typeof(UserAccountEmail));
		AssertProtectedParameterlessConstructor(typeof(UserAccountRole));
		AssertProtectedParameterlessConstructor(typeof(UserAccountCredential));

		Assert.Equal(typeof(IReadOnlyCollection<UserAccountEmail>), typeof(UserAccount).GetProperty(nameof(UserAccount.Emails))?.PropertyType);
		Assert.Equal(typeof(IReadOnlyCollection<UserAccountRole>), typeof(UserAccount).GetProperty(nameof(UserAccount.Roles))?.PropertyType);
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
