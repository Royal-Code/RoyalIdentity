using RoyalCode.SmartProblems;
using RoyalIdentity.UserAccounts.Features.Accounts.Domain;
using RoyalIdentity.UserAccounts.Options;

namespace Tests.UserAccounts;

public class UserAccountDomainTests
{
	private static readonly DateTimeOffset Now = new(2026, 6, 18, 10, 0, 0, TimeSpan.Zero);
	private static readonly TestPasswordHasher PasswordHasher = new();

	[Fact]
	public void Create_GeneratesSubject_WhenSubjectIsNotProvided()
	{
		var account = CreateAccount();

		Assert.Equal("realm-a", account.RealmId);
		Assert.Equal("subject-1", account.SubjectId);
		Assert.Equal("alice", account.Username);
		Assert.Equal("ALICE", account.NormalizedUsername);
		Assert.Equal(AccountStatus.Active, account.Status);
	}

	[Fact]
	public void Create_RejectsProvidedSubject_WhenPolicyDisallowsIt()
	{
		var result = UserAccount.Create(
			"realm-a",
			"alice",
			"Alice",
			new UserAccountsRealmOptions(),
			Now,
			subjectId: "provided-subject");

		Assert.True(result.IsFailure);
	}

	[Fact]
	public void Create_AcceptsProvidedSubject_WhenPolicyAllowsIt()
	{
		var account = Value(UserAccount.Create(
			"realm-a",
			"alice",
			"Alice",
			new UserAccountsRealmOptions { AllowProvidedSubjectId = true },
			Now,
			subjectId: "provided-subject"));

		Assert.Equal("provided-subject", account.SubjectId);
	}

	[Fact]
	public void ChangeUsername_DoesNotChangeSubjectId()
	{
		var account = CreateAccount();
		var subject = account.SubjectId;

		var result = account.ChangeUsername("alice.renamed", Now.AddMinutes(1));

		Assert.True(result.IsSuccess);
		Assert.Equal(subject, account.SubjectId);
		Assert.Equal("alice.renamed", account.Username);
		Assert.Equal("ALICE.RENAMED", account.NormalizedUsername);
	}

	[Fact]
	public void AddEmail_MaintainsSinglePrimary_AndStoresVerificationAndFictitiousFlags()
	{
		var account = CreateAccount();

		Assert.True(account.AddEmail("alice@example.com", Now, isPrimary: true, isVerified: true).IsSuccess);
		Assert.True(account.AddEmail("subject-1@fictitious.local", Now.AddMinutes(1), isPrimary: true, isFictitious: true).IsSuccess);

		Assert.Single(account.Emails, e => e.IsPrimary);
		Assert.Equal("subject-1@fictitious.local", account.PrimaryEmail?.Address);
		Assert.Contains(account.Emails, e => e.Address == "alice@example.com" && e.IsVerified && !e.IsPrimary);
		Assert.Contains(account.Emails, e => e.Address == "subject-1@fictitious.local" && e.IsFictitious && e.IsPrimary);
	}

	[Fact]
	public void AddRole_TreatsRolesAsFirstClassValues_AndRejectsDuplicates()
	{
		var account = CreateAccount();

		Assert.True(account.AddRole("admin", Now).IsSuccess);
		Assert.True(account.AddRole("Admin", Now.AddMinutes(1)).IsFailure);

		var role = Assert.Single(account.Roles);
		Assert.Equal("admin", role.Name);
		Assert.Equal("ADMIN", role.NormalizedName);
	}

	[Fact]
	public void SetPassword_RejectsBasicComplexityViolations()
	{
		var account = CreateAccount();
		var options = RelaxedPasswordOptions();
		options.MinimumLength = 10;
		options.RequireDigit = true;

		var tooShort = account.SetPassword("Aa1!", options, PasswordHasher, Now);
		var withoutDigit = account.SetPassword("Valid-pass", options, PasswordHasher, Now);
		var valid = account.SetPassword("Valid-pass1", options, PasswordHasher, Now);

		Assert.True(tooShort.IsFailure);
		Assert.True(withoutDigit.IsFailure);
		Assert.True(valid.IsSuccess);
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
		Assert.True(account.SetPassword("Valid-pass1", options, PasswordHasher, Now).IsSuccess);

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
	public void AuthenticateLocal_MapsAccountStatusFailures()
	{
		var inactive = CreateAccount();
		var blocked = CreateAccount();
		var options = RelaxedPasswordOptions();
		Assert.True(inactive.SetPassword("Valid-pass1", options, PasswordHasher, Now).IsSuccess);
		Assert.True(blocked.SetPassword("Valid-pass1", options, PasswordHasher, Now).IsSuccess);

		inactive.Deactivate(Now.AddMinutes(1));
		blocked.Block(Now.AddMinutes(1));

		var inactiveResult = inactive.AuthenticateLocal("Valid-pass1", options, PasswordHasher, Now.AddMinutes(2));
		var blockedResult = blocked.AuthenticateLocal("Valid-pass1", options, PasswordHasher, Now.AddMinutes(2));

		Assert.False(inactiveResult.Success);
		Assert.Equal(LocalAuthenticationFailureReason.Inactive, inactiveResult.Reason);
		Assert.False(blockedResult.Success);
		Assert.Equal(LocalAuthenticationFailureReason.Blocked, blockedResult.Reason);
	}

	private static UserAccount CreateAccount()
	{
		return Value(UserAccount.Create(
			"realm-a",
			"alice",
			"Alice",
			new UserAccountsRealmOptions(),
			Now,
			subjectIdGenerator: () => "subject-1"));
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

	private static T Value<T>(Result<T> result)
	{
		return result.Match(
			value => value,
			problems => throw new InvalidOperationException(problems.ToString()));
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
