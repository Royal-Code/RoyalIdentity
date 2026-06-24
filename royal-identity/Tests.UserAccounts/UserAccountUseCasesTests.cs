using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RoyalIdentity.UserAccounts.Features.Accounts.Commons;
using RoyalIdentity.UserAccounts.Features.Accounts.Domain;
using RoyalIdentity.UserAccounts.Features.Accounts.UseCases;
using RoyalIdentity.UserAccounts.Features.ScopeProperties.Commons;
using RoyalIdentity.UserAccounts.Features.ScopeProperties.Domain;
using RoyalIdentity.UserAccounts.Features.ScopeProperties.UseCases;
using RoyalIdentity.UserAccounts.Options;
using RoyalIdentity.UserAccounts.Sqlite;

namespace Tests.UserAccounts;

public class UserAccountUseCasesTests
{
	private static readonly DateTimeOffset Start = new(2026, 6, 20, 12, 0, 0, TimeSpan.Zero);

	// ---- Create: subject id policy ----

	[Fact]
	public async Task Create_GeneratesSubjectId_WhenNotProvided()
	{
		await using var provider = BuildProvider();

		var result = await CreateAsync(provider, NewCreate("r1", "alice"));

		Assert.True(result.IsSuccess);
		Assert.True(result.HasValue(out var account));
		Assert.False(string.IsNullOrWhiteSpace(account!.SubjectId));
	}

	[Fact]
	public async Task Create_AcceptsDeterministicSubjectId_WhenRealmAllows()
	{
		await using var provider = BuildProvider();
		var options = Options();
		options.AllowProvidedSubjectId = true;

		var result = await CreateAsync(provider, NewCreate("r1", "alice", options, subjectId: "deterministic-sub"));

		Assert.True(result.IsSuccess);
		Assert.True(result.HasValue(out var account));
		Assert.Equal("deterministic-sub", account!.SubjectId);
	}

	[Fact]
	public async Task Create_RejectsProvidedSubjectId_WhenRealmDisallows()
	{
		await using var provider = BuildProvider();

		var result = await CreateAsync(provider, NewCreate("r1", "alice", subjectId: "provided"));

		Assert.True(result.IsFailure);
	}

	[Fact]
	public async Task Create_RejectsDuplicateSubject_InSameRealm()
	{
		await using var provider = BuildProvider();
		var options = Options();
		options.AllowProvidedSubjectId = true;

		Assert.True((await CreateAsync(provider, NewCreate("r1", "alice", options, subjectId: "dup"))).IsSuccess);
		var second = await CreateAsync(provider, NewCreate("r1", "bob", options, subjectId: "dup"));

		Assert.True(second.IsFailure);
	}

	[Fact]
	public async Task Create_AllowsSameSubject_InDifferentRealms()
	{
		await using var provider = BuildProvider();
		var options = Options();
		options.AllowProvidedSubjectId = true;

		var first = await CreateAsync(provider, NewCreate("r1", "alice", options, subjectId: "shared"));
		var second = await CreateAsync(provider, NewCreate("r2", "alice", options, subjectId: "shared"));

		Assert.True(first.IsSuccess);
		Assert.True(second.IsSuccess);
	}

	[Fact]
	public async Task Create_RejectsDuplicateEmail_WhenRealmDisallowsDuplicates()
	{
		await using var provider = BuildProvider();

		Assert.True((await CreateAsync(provider, NewCreate("r1", "alice", email: "shared@example.com"))).IsSuccess);
		var second = await CreateAsync(provider, NewCreate("r1", "bob", email: "Shared@Example.com"));

		Assert.True(second.IsFailure);
	}

	[Fact]
	public async Task Create_MaterializesFictitiousPrimaryEmail_PerRealmPolicy()
	{
		await using var provider = BuildProvider();
		var options = Options();
		options.AllowFictitiousEmail = true;
		options.FictitiousEmailIsVerifiedByDefault = true;
		options.AllowProvidedSubjectId = true;

		var result = await CreateAsync(provider, NewCreate("r1", "alice", options, subjectId: "sub-fic"));

		Assert.True(result.IsSuccess);
		Assert.True(result.HasValue(out var account));
		var email = Assert.Single(account!.Emails);
		Assert.True(email.IsFictitious);
		Assert.True(email.IsPrimary);
		Assert.True(email.IsVerified);
		Assert.Equal("sub-fic@fictitious.local", email.Address);
	}

	// ---- Reader: realm isolation & login resolution ----

	[Fact]
	public async Task FindBySubjectId_DoesNotReturnAccountFromAnotherRealm()
	{
		await using var provider = BuildProvider();
		var options = Options();
		options.AllowProvidedSubjectId = true;
		await CreateAsync(provider, NewCreate("r1", "alice", options, subjectId: "s"));

		using var scope = provider.CreateScope();
		var reader = scope.ServiceProvider.GetRequiredService<UserAccountReader>();

		Assert.NotNull(await reader.FindBySubjectIdAsync("r1", "s"));
		Assert.Null(await reader.FindBySubjectIdAsync("r2", "s"));
	}

	[Fact]
	public async Task FindByLogin_ByUsername_RespectsNormalization()
	{
		await using var provider = BuildProvider();
		await CreateAsync(provider, NewCreate("r1", "Alice"));

		using var scope = provider.CreateScope();
		var reader = scope.ServiceProvider.GetRequiredService<UserAccountReader>();

		Assert.NotNull(await reader.FindByLoginAsync("r1", "alice", Options()));
		Assert.NotNull(await reader.FindByLoginAsync("r1", "  ALICE ", Options()));
		Assert.Null(await reader.FindByLoginAsync("r1", "bob", Options()));
	}

	[Fact]
	public async Task FindByLogin_ByEmail_HonorsLoginPolicyAndVerification()
	{
		await using var provider = BuildProvider();
		var loginWithEmail = Options();
		loginWithEmail.LoginWithEmail = true;
		await CreateAsync(provider, NewCreate("r1", "alice", loginWithEmail, email: "alice@example.com", emailVerified: true));

		using var scope = provider.CreateScope();
		var reader = scope.ServiceProvider.GetRequiredService<UserAccountReader>();

		// email login enabled -> resolves
		Assert.NotNull(await reader.FindByLoginAsync("r1", "ALICE@example.com", loginWithEmail));

		// email login disabled -> not resolved by email
		var noEmailLogin = Options();
		Assert.Null(await reader.FindByLoginAsync("r1", "alice@example.com", noEmailLogin));

		// verification enforced + unverified email -> not resolved
		await CreateAsync(provider, NewCreate("r1", "bob", loginWithEmail, email: "bob@example.com", emailVerified: false));
		var verifyRequired = Options();
		verifyRequired.LoginWithEmail = true;
		verifyRequired.VerifyEmail = true;
		Assert.Null(await reader.FindByLoginAsync("r1", "bob@example.com", verifyRequired));
	}

	[Fact]
	public async Task FindByLogin_ByEmail_AllowsUnverifiedEmail_WhenVerificationIsNotRequired()
	{
		await using var provider = BuildProvider();
		var loginWithEmail = Options();
		loginWithEmail.LoginWithEmail = true;
		loginWithEmail.AllowProvidedSubjectId = true;
		await CreateAsync(provider, NewCreate(
			"r1",
			"alice",
			loginWithEmail,
			subjectId: "alice",
			email: "alice@example.com",
			emailVerified: false));

		using var scope = provider.CreateScope();
		var reader = scope.ServiceProvider.GetRequiredService<UserAccountReader>();

		var found = await reader.FindByLoginAsync("r1", "alice@example.com", loginWithEmail);

		Assert.NotNull(found);
		Assert.Equal("alice", found!.SubjectId);
	}

	[Fact]
	public async Task FindByLogin_ByEmailAsUsername_ResolvesByEmail()
	{
		await using var provider = BuildProvider();
		var emailAsUsername = Options();
		emailAsUsername.EmailAsUsername = true;
		await CreateAsync(provider, NewCreate("r1", "ignored", emailAsUsername, email: "alice@example.com", emailVerified: true));

		using var scope = provider.CreateScope();
		var reader = scope.ServiceProvider.GetRequiredService<UserAccountReader>();

		var found = await reader.FindByLoginAsync("r1", "ALICE@example.com", emailAsUsername);

		Assert.NotNull(found);
		Assert.Equal("alice@example.com", found!.Username);
	}

	[Fact]
	public async Task FindByLogin_ByEmail_PrefersPrimaryEmail_WhenDuplicateRowsExist()
	{
		await using var provider = BuildProvider();

		using (var scope = provider.CreateScope())
		{
			var db = scope.ServiceProvider.GetRequiredService<UserAccountsSqliteDbContext>();
			var nonPrimaryAccount = new UserAccount("r1", "non-primary", "nonprimary", "NONPRIMARY", "Non Primary", Start);
			Assert.True(nonPrimaryAccount.AddEmail(
				new UserAccountEmail("r1", "owner@example.com", "OWNER@EXAMPLE.COM", true, true, false),
				Start).IsSuccess);
			Assert.True(nonPrimaryAccount.AddEmail(
				new UserAccountEmail("r1", "shared@example.com", "SHARED@EXAMPLE.COM", false, true, false),
				Start).IsSuccess);

			var primaryAccount = new UserAccount("r1", "primary", "primary", "PRIMARY", "Primary", Start);
			Assert.True(primaryAccount.AddEmail(
				new UserAccountEmail("r1", "shared@example.com", "SHARED@EXAMPLE.COM", true, true, false),
				Start).IsSuccess);

			db.UserAccounts.AddRange(nonPrimaryAccount, primaryAccount);
			await db.SaveChangesAsync();
		}

		using var readScope = provider.CreateScope();
		var reader = readScope.ServiceProvider.GetRequiredService<UserAccountReader>();
		var options = Options();
		options.LoginWithEmail = true;

		var found = await reader.FindByLoginAsync("r1", "shared@example.com", options);

		Assert.NotNull(found);
		Assert.Equal("primary", found!.SubjectId);
	}

	// ---- Authentication: internal reasons ----

	[Fact]
	public async Task Authenticate_ReturnsCorrectInternalReason_ForEachOutcome()
	{
		await using var provider = BuildProvider();
		var options = Options();
		options.AllowProvidedSubjectId = true;
		options.PasswordOptions.MaxFailedAccessAttempts = 2;
		options.PasswordOptions.AccountLockoutDurationMinutes = 10;

		// not found
		Assert.Equal(LocalAuthenticationFailureReason.NotFound, (await AuthReason(provider, options, "ghost", "secret")));

		// password not set
		await CreateAsync(provider, NewCreate("r1", "nopass", options, subjectId: "nopass", password: null));
		Assert.Equal(LocalAuthenticationFailureReason.PasswordNotSet, await AuthReason(provider, options, "nopass", "secret"));

		// valid account with password
		await CreateAsync(provider, NewCreate("r1", "alice", options, subjectId: "alice", password: "secret"));

		// invalid credentials
		Assert.Equal(LocalAuthenticationFailureReason.InvalidCredentials, await AuthReason(provider, options, "alice", "wrong"));

		// lockout after reaching the limit (2nd failure locks, 3rd is rejected as locked out)
		Assert.Equal(LocalAuthenticationFailureReason.InvalidCredentials, await AuthReason(provider, options, "alice", "wrong"));
		Assert.Equal(LocalAuthenticationFailureReason.LockedOut, await AuthReason(provider, options, "alice", "secret"));

		// inactive
		await MutateAccountAsync(provider, "r1", "alice", a => a.Deactivate(Start));
		Assert.Equal(LocalAuthenticationFailureReason.Inactive, await AuthReason(provider, options, "alice", "secret"));

		// blocked (reactivate, then block)
		await MutateAccountAsync(provider, "r1", "alice", a =>
		{
			a.Activate(Start);
			a.UnlockLocalCredential(Start);
			a.Block("administrative", Start);
		});
		Assert.Equal(LocalAuthenticationFailureReason.Blocked, await AuthReason(provider, options, "alice", "secret"));
	}

	[Fact]
	public async Task Authenticate_Succeeds_WithValidCredentials()
	{
		await using var provider = BuildProvider();
		var options = Options();
		options.AllowProvidedSubjectId = true;
		await CreateAsync(provider, NewCreate("r1", "alice", options, subjectId: "alice", password: "secret"));

		var result = await AuthenticateAsync(provider, options, "alice", "secret");

		Assert.True(result.IsSuccess);
		Assert.True(result.HasValue(out var outcome));
		Assert.True(outcome!.Success);
		Assert.Equal("alice", outcome.SubjectId);
	}

	// ---- Change password ----

	[Fact]
	public async Task ChangePassword_ReplacesCredential_AfterVerifyingCurrent()
	{
		await using var provider = BuildProvider();
		var options = Options();
		options.AllowProvidedSubjectId = true;
		await CreateAsync(provider, NewCreate("r1", "alice", options, subjectId: "alice", password: "secret"));

		using (var scope = provider.CreateScope())
		{
			var handler = scope.ServiceProvider.GetRequiredService<IChangeUserAccountPasswordHandler>();
			var result = await handler.HandleAsync(new ChangeUserAccountPassword
			{
				RealmId = "r1",
				Options = options,
				SubjectId = "alice",
				CurrentPassword = "secret",
				NewPassword = "renewed"
			}, default);
			Assert.True(result.IsSuccess);
		}

		Assert.True((await AuthenticateAsync(provider, options, "alice", "renewed")).HasValue(out var ok) && ok!.Success);
		Assert.Equal(LocalAuthenticationFailureReason.InvalidCredentials, await AuthReason(provider, options, "alice", "secret"));
	}

	[Fact]
	public async Task ChangePassword_RejectsReuse_OfCurrentAndRecentPasswords_WhenHistoryEnforced()
	{
		await using var provider = BuildProvider();
		var options = Options();
		options.AllowProvidedSubjectId = true;
		options.PasswordOptions.EnforcePasswordHistory = true;
		options.PasswordOptions.PasswordHistoryCount = 3;
		options.PasswordOptions.MaxPasswordHistoryComparisons = 24;
		await CreateAsync(provider, NewCreate("r1", "alice", options, subjectId: "alice", password: "secret"));

		// reusing the current password is rejected
		Assert.True((await ChangeAsync(provider, options, "alice", "secret", "secret")).IsFailure);

		// move to a new password, then try to go back to the previous one (now in history)
		Assert.True((await ChangeAsync(provider, options, "alice", "secret", "renewed")).IsSuccess);
		Assert.True((await ChangeAsync(provider, options, "alice", "renewed", "secret")).IsFailure);

		// a brand-new password is accepted
		Assert.True((await ChangeAsync(provider, options, "alice", "renewed", "fresh-one")).IsSuccess);
	}

	[Fact]
	public async Task ChangeOwnPassword_HonorsAllowChangePassword_Toggle()
	{
		await using var provider = BuildProvider();
		var allowed = Options();
		allowed.AllowProvidedSubjectId = true;
		allowed.AllowChangePassword = true;
		await CreateAsync(provider, NewCreate("r1", "alice", allowed, subjectId: "alice", password: "secret"));

		// allowed -> the user-facing change succeeds and the new password authenticates
		Assert.True((await ChangeOwnAsync(provider, allowed, "alice", "secret", "renewed")).IsSuccess);
		Assert.True((await AuthenticateAsync(provider, allowed, "alice", "renewed")).HasValue(out var ok) && ok!.Success);

		// disabled -> rejected before any mutation; the current password is unchanged
		var disabled = Options();
		disabled.AllowChangePassword = false;
		Assert.True((await ChangeOwnAsync(provider, disabled, "alice", "renewed", "another")).IsFailure);
		Assert.True((await AuthenticateAsync(provider, disabled, "alice", "renewed")).HasValue(out var still) && still!.Success);
	}

	[Fact]
	public async Task Authenticate_SurfacesRequiredAction_WhenMustChangePassword()
	{
		await using var provider = BuildProvider();
		var options = Options();
		options.AllowProvidedSubjectId = true;
		await CreateAsync(provider, NewCreate("r1", "alice", options, subjectId: "alice", password: "secret"));
		await MutateAccountAsync(provider, "r1", "alice",
			a => a.SetPassword("hashed:secret", Start, options.PasswordOptions, mustChangePassword: true));

		var result = await AuthenticateAsync(provider, options, "alice", "secret");

		Assert.True(result.HasValue(out var outcome));
		Assert.False(outcome!.Success);
		Assert.Null(outcome.Reason);
		Assert.Equal(LocalRequiredAction.ChangePasswordMustChange, outcome.RequiredAction);
		Assert.Equal("alice", outcome.SubjectId);
	}

	// ---- Scope properties + claims ----

	[Fact]
	public async Task SetScopeProperty_Validates_AgainstActiveVersion_AndFailsForInactiveScope()
	{
		await using var provider = BuildProvider();
		var options = Options();
		options.AllowProvidedSubjectId = true;
		await CreateAsync(provider, NewCreate("r1", "alice", options, subjectId: "alice"));
		await SeedScopeAsync(provider, "r1", "profile", "nickname", new PropertyDefinitionSettings
		{
			ValueType = PropertyValueType.Text,
			IsActive = true
		});

		// valid write against the active version
		Assert.True((await SetPropertyAsync(provider, "r1", "alice", "profile", "nickname", ["nick"])).IsSuccess);

		// undefined claim type -> failure
		Assert.True((await SetPropertyAsync(provider, "r1", "alice", "profile", "unknown", ["x"])).IsFailure);

		// inactive scope -> failure
		await DeactivateScopeAsync(provider, "r1", "profile");
		Assert.True((await SetPropertyAsync(provider, "r1", "alice", "profile", "nickname", ["nick2"])).IsFailure);
	}

	[Fact]
	public async Task SetScopeProperty_Fails_ForScopeWithoutActiveVersion()
	{
		await using var provider = BuildProvider();
		var options = Options();
		options.AllowProvidedSubjectId = true;
		await CreateAsync(provider, NewCreate("r1", "alice", options, subjectId: "alice"));

		// a draft-only scope (never approved) has no active version
		using (var scope = provider.CreateScope())
		{
			var db = scope.ServiceProvider.GetRequiredService<UserAccountsSqliteDbContext>();
			var draft = new PropertyScope("r1", "profile", "Profile", Start);
			draft.AddDefinition(draft.Versions.Single(), "nickname", new PropertyDefinitionSettings { ValueType = PropertyValueType.Text });
			db.PropertyScopes.Add(draft);
			await db.SaveChangesAsync();
		}

		Assert.True((await SetPropertyAsync(provider, "r1", "alice", "profile", "nickname", ["nick"])).IsFailure);
	}

	[Fact]
	public async Task GetClaims_CombinesFixedFields_Roles_AndDynamicValues()
	{
		await using var provider = BuildProvider();
		var options = Options();
		options.AllowProvidedSubjectId = true;
		await CreateAsync(provider, NewCreate("r1", "alice", options, subjectId: "alice",
			displayName: "Alice Liddell", email: "alice@example.com", emailVerified: true, roles: ["admin"]));
		await SeedScopeAsync(provider, "r1", "profile", "nickname", new PropertyDefinitionSettings
		{
			ValueType = PropertyValueType.Text,
			IsActive = true
		});
		Assert.True((await SetPropertyAsync(provider, "r1", "alice", "profile", "nickname", ["ally"])).IsSuccess);

		using var scope = provider.CreateScope();
		var claimsReader = scope.ServiceProvider.GetRequiredService<UserAccountClaimsReader>();

		var claims = await claimsReader.GetClaimsAsync(
			"r1", "alice", options,
			["profile", "email"],
			["preferred_username", "name", "role", "email", "email_verified", "nickname"]);

		Assert.Contains(claims, c => c is { ScopeName: "profile", ClaimType: "preferred_username", Value: "alice" });
		Assert.Contains(claims, c => c is { ScopeName: "profile", ClaimType: "name", Value: "Alice Liddell" });
		Assert.Contains(claims, c => c is { ScopeName: "profile", ClaimType: "role", Value: "admin" });
		Assert.Contains(claims, c => c is { ScopeName: "email", ClaimType: "email", Value: "alice@example.com" });
		Assert.Contains(claims, c => c is { ScopeName: "email", ClaimType: "email_verified", Value: "true" });
		Assert.Contains(claims, c => c is { ScopeName: "profile", ClaimType: "nickname", Value: "ally" });
	}

	[Fact]
	public async Task GetClaims_ReturnsNothing_ForInactiveAccount()
	{
		await using var provider = BuildProvider();
		var options = Options();
		options.AllowProvidedSubjectId = true;
		await CreateAsync(provider, NewCreate("r1", "alice", options, subjectId: "alice"));
		await MutateAccountAsync(provider, "r1", "alice", a => a.Deactivate(Start));

		using var scope = provider.CreateScope();
		var claimsReader = scope.ServiceProvider.GetRequiredService<UserAccountClaimsReader>();

		var claims = await claimsReader.GetClaimsAsync("r1", "alice", options, ["profile"], ["preferred_username"]);

		Assert.Empty(claims);
	}

	[Fact]
	public async Task ValueTypeChangeGuard_BlocksChange_WhenPersistedValuesExist()
	{
		await using var provider = BuildProvider();
		var options = Options();
		options.AllowProvidedSubjectId = true;
		await CreateAsync(provider, NewCreate("r1", "alice", options, subjectId: "alice"));
		var definitionId = await SeedScopeAsync(provider, "r1", "profile", "level", new PropertyDefinitionSettings
		{
			ValueType = PropertyValueType.Integer,
			IsActive = true
		});
		Assert.True((await SetPropertyAsync(provider, "r1", "alice", "profile", "level", ["3"])).IsSuccess);

		using var scope = provider.CreateScope();
		var guard = scope.ServiceProvider.GetRequiredService<PropertyValueTypeChangeGuard>();

		Assert.True((await guard.EnsureValueTypeChangeAllowedAsync(definitionId, PropertyValueType.Text)).IsFailure);
		Assert.True((await guard.EnsureValueTypeChangeAllowedAsync(definitionId, PropertyValueType.Integer)).IsSuccess);
	}

	// ---- harness ----

	private static ServiceProvider BuildProvider()
	{
		var services = new ServiceCollection();
		services.AddSingleton<TimeProvider>(new FakeClock(Start));
		services.AddSingleton<IUserAccountPasswordHasher, FakeHasher>();
		services.AddUserAccountsSqliteInMemory();
		return services.BuildServiceProvider();
	}

	private static UserAccountsRealmOptions Options() => UserAccountsTestOptions.Relaxed();

	private static CreateUserAccount NewCreate(
		string realmId,
		string username,
		UserAccountsRealmOptions? options = null,
		string? subjectId = null,
		string? displayName = null,
		string? email = null,
		bool emailVerified = false,
		string? password = "secret",
		IReadOnlyList<string>? roles = null)
	{
		return new CreateUserAccount
		{
			RealmId = realmId,
			Options = options ?? Options(),
			Username = username,
			DisplayName = displayName,
			Email = email,
			EmailVerified = emailVerified,
			Password = password,
			SubjectId = subjectId,
			Roles = roles
		};
	}

	private static async Task<RoyalCode.SmartProblems.Result<UserAccount>> CreateAsync(
		ServiceProvider provider, CreateUserAccount command)
	{
		using var scope = provider.CreateScope();
		var handler = scope.ServiceProvider.GetRequiredService<ICreateUserAccountHandler>();
		return await handler.HandleAsync(command, default);
	}

	private static async Task<RoyalCode.SmartProblems.Result<LocalAuthenticationResult>> AuthenticateAsync(
		ServiceProvider provider, UserAccountsRealmOptions options, string login, string password)
	{
		using var scope = provider.CreateScope();
		var handler = scope.ServiceProvider.GetRequiredService<IAuthenticateLocalCredentialHandler>();
		return await handler.HandleAsync(new AuthenticateLocalCredential
		{
			RealmId = "r1",
			Options = options,
			Login = login,
			Password = password
		}, default);
	}

	private static async Task<LocalAuthenticationFailureReason> AuthReason(
		ServiceProvider provider, UserAccountsRealmOptions options, string login, string password)
	{
		var result = await AuthenticateAsync(provider, options, login, password);
		Assert.True(result.HasValue(out var outcome));
		Assert.False(outcome!.Success);
		Assert.NotNull(outcome.Reason);
		return outcome.Reason!.Value;
	}

	private static async Task<RoyalCode.SmartProblems.Result> ChangeAsync(
		ServiceProvider provider, UserAccountsRealmOptions options, string subjectId, string currentPassword, string newPassword)
	{
		using var scope = provider.CreateScope();
		var handler = scope.ServiceProvider.GetRequiredService<IChangeUserAccountPasswordHandler>();
		return await handler.HandleAsync(new ChangeUserAccountPassword
		{
			RealmId = "r1",
			Options = options,
			SubjectId = subjectId,
			CurrentPassword = currentPassword,
			NewPassword = newPassword
		}, default);
	}

	private static async Task<RoyalCode.SmartProblems.Result> ChangeOwnAsync(
		ServiceProvider provider, UserAccountsRealmOptions options, string subjectId, string currentPassword, string newPassword)
	{
		using var scope = provider.CreateScope();
		var handler = scope.ServiceProvider.GetRequiredService<IChangeOwnPasswordHandler>();
		return await handler.HandleAsync(new ChangeOwnPassword
		{
			RealmId = "r1",
			Options = options,
			SubjectId = subjectId,
			CurrentPassword = currentPassword,
			NewPassword = newPassword
		}, default);
	}

	private static async Task<RoyalCode.SmartProblems.Result> SetPropertyAsync(
		ServiceProvider provider, string realmId, string subjectId, string scopeName, string claimType, IReadOnlyList<string> values)
	{
		using var scope = provider.CreateScope();
		var handler = scope.ServiceProvider.GetRequiredService<ISetUserAccountScopePropertyHandler>();
		return await handler.HandleAsync(new SetUserAccountScopeProperty
		{
			RealmId = realmId,
			SubjectId = subjectId,
			ScopeName = scopeName,
			ClaimType = claimType,
			Values = values
		}, default);
	}

	private static async Task<long> SeedScopeAsync(
		ServiceProvider provider, string realmId, string scopeName, string claimType, PropertyDefinitionSettings settings)
	{
		using var scope = provider.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<UserAccountsSqliteDbContext>();
		var propertyScope = new PropertyScope(realmId, scopeName, scopeName, Start);
		var version = propertyScope.Versions.Single();
		Assert.True(propertyScope.AddDefinition(version, claimType, settings).IsSuccess);
		Assert.True(propertyScope.ApproveVersion(version, Start).IsSuccess);
		db.PropertyScopes.Add(propertyScope);
		await db.SaveChangesAsync();
		return propertyScope.ActiveVersion!.DefinitionVersions.Single().PropertyDefinitionId;
	}

	private static async Task DeactivateScopeAsync(ServiceProvider provider, string realmId, string scopeName)
	{
		using var scope = provider.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<UserAccountsSqliteDbContext>();
		var propertyScope = await db.PropertyScopes.FirstAsync(s => s.RealmId == realmId && s.Name == scopeName);
		propertyScope.Deactivate();
		await db.SaveChangesAsync();
	}

	private static async Task MutateAccountAsync(
		ServiceProvider provider, string realmId, string subjectId, Action<UserAccount> mutate)
	{
		using var scope = provider.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<UserAccountsSqliteDbContext>();
		var account = await db.UserAccounts
			.Include(a => a.LocalCredential)
			.FirstAsync(a => a.RealmId == realmId && a.SubjectId == subjectId);
		mutate(account);
		await db.SaveChangesAsync();
	}

	private sealed class FakeClock(DateTimeOffset start) : TimeProvider
	{
		public DateTimeOffset Now { get; set; } = start;

		public override DateTimeOffset GetUtcNow() => Now;
	}

	private sealed class FakeHasher : IUserAccountPasswordHasher
	{
		public string Hash(string password) => $"hashed:{password}";

		public bool Verify(string password, string passwordHash) => passwordHash == Hash(password);
	}
}
