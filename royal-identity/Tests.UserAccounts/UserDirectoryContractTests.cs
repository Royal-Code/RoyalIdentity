using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.Extensions.DependencyInjection;
using RoyalIdentity.Models;
using RoyalIdentity.Options;
using RoyalIdentity.Security.Passwords;
using RoyalIdentity.Storage.InMemory;
using RoyalIdentity.Storage.InMemory.Extensions;
using RoyalIdentity.UserAccounts.Features.Accounts.Domain;
using RoyalIdentity.UserAccounts.Infrastructure.Data;
using RoyalIdentity.UserAccounts.Integration;
using RoyalIdentity.UserAccounts.Options;
using RoyalIdentity.UserAccounts.Sqlite;
using RoyalIdentity.Users;
using RoyalIdentity.Users.Contracts;
using RoyalIdentity.Users.Defaults;

namespace Tests.UserAccounts;

/// <summary>
/// Fase 10 contract tests for the IdP user edge. The same behavioral contract runs against the
/// in-memory fake and the real UserAccounts module adapter.
/// </summary>
public abstract class UserDirectoryContractTests
{
	private static readonly DateTimeOffset Start = new(2026, 6, 20, 12, 0, 0, TimeSpan.Zero);

	[Fact]
	public async Task SubjectStore_ResolvesSeededSubjects_AndIsRealmScoped()
	{
		await using var harness = await CreateHarnessAsync();
		using var scope = harness.Provider.CreateScope();

		var directory = scope.ServiceProvider.GetRequiredService<IUserDirectory>();
		var subject = await directory.GetSubjectStore(harness.PrimaryRealm)
			.FindBySubjectIdAsync(MemoryStorage.AliceSubjectId);

		Assert.NotNull(subject);
		Assert.Equal(MemoryStorage.AliceSubjectId, subject!.SubjectId);
		Assert.Equal("Alice", subject.DisplayName);
		Assert.True(subject.IsActive);

		var otherRealmSubject = await directory.GetSubjectStore(harness.OtherRealm)
			.FindBySubjectIdAsync(MemoryStorage.AliceSubjectId);

		Assert.Null(otherRealmSubject);
	}

	[Fact]
	public async Task LocalAuthenticator_AuthenticatesSeededUsers_ByUsername()
	{
		await using var harness = await CreateHarnessAsync();
		using var scope = harness.Provider.CreateScope();

		var authenticator = scope.ServiceProvider
			.GetRequiredService<IUserDirectory>()
			.GetLocalAuthenticator(harness.PrimaryRealm);

		var alice = await authenticator.AuthenticateLocalAsync("alice", "alice");
		var bob = await authenticator.AuthenticateLocalAsync("bob", "bob");

		Assert.True(alice.Success);
		Assert.Equal(MemoryStorage.AliceSubjectId, alice.Subject!.SubjectId);
		Assert.Equal("Alice", alice.Subject.DisplayName);

		Assert.True(bob.Success);
		Assert.Equal(MemoryStorage.BobSubjectId, bob.Subject!.SubjectId);
		Assert.Equal("Bob", bob.Subject.DisplayName);
	}

	[Fact]
	public async Task LocalAuthenticator_IsRealmScoped()
	{
		await using var harness = await CreateHarnessAsync();
		using var scope = harness.Provider.CreateScope();

		var result = await scope.ServiceProvider
			.GetRequiredService<IUserDirectory>()
			.GetLocalAuthenticator(harness.OtherRealm)
			.AuthenticateLocalAsync("alice", "alice");

		Assert.False(result.Success);
		Assert.Equal(AuthenticationFailureReason.NotFound, result.Reason);
	}

	[Fact]
	public async Task LocalAuthenticator_MapsInactiveAndPasswordlessFailures()
	{
		await using var harness = await CreateHarnessAsync();
		await SeedAsync(harness, new UserSeed(
			harness.PrimaryRealm,
			"inactive-subject",
			"inactive",
			"Inactive",
			"inactive@example.com",
			"inactive",
			IsActive: false,
			Roles: ["user"]));
		await SeedAsync(harness, new UserSeed(
			harness.PrimaryRealm,
			"passwordless-subject",
			"passwordless",
			"Passwordless",
			"passwordless@example.com",
			Password: null,
			IsActive: true,
			Roles: ["user"]));

		using var scope = harness.Provider.CreateScope();
		var authenticator = scope.ServiceProvider
			.GetRequiredService<IUserDirectory>()
			.GetLocalAuthenticator(harness.PrimaryRealm);

		var inactive = await authenticator.AuthenticateLocalAsync("inactive", "inactive");
		var passwordless = await authenticator.AuthenticateLocalAsync("passwordless", "passwordless");

		Assert.False(inactive.Success);
		Assert.Equal(AuthenticationFailureReason.Inactive, inactive.Reason);
		Assert.Null(inactive.Subject);

		Assert.False(passwordless.Success);
		Assert.Equal(AuthenticationFailureReason.InvalidCredentials, passwordless.Reason);
		Assert.Null(passwordless.Subject);
	}

	[Fact]
	public async Task LocalAuthenticator_FailureCounterResetsAfterSuccess()
	{
		await using var harness = await CreateHarnessAsync();
		using var scope = harness.Provider.CreateScope();

		var authenticator = scope.ServiceProvider
			.GetRequiredService<IUserDirectory>()
			.GetLocalAuthenticator(harness.PrimaryRealm);

		Assert.Equal(AuthenticationFailureReason.InvalidCredentials,
			(await authenticator.AuthenticateLocalAsync("alice", "wrong")).Reason);

		Assert.True((await authenticator.AuthenticateLocalAsync("alice", "alice")).Success);

		Assert.Equal(AuthenticationFailureReason.InvalidCredentials,
			(await authenticator.AuthenticateLocalAsync("alice", "wrong-1")).Reason);
		Assert.Equal(AuthenticationFailureReason.InvalidCredentials,
			(await authenticator.AuthenticateLocalAsync("alice", "wrong-2")).Reason);

		var stillAllowed = await authenticator.AuthenticateLocalAsync("alice", "alice");

		Assert.True(stillAllowed.Success);
	}

	[Fact]
	public async Task LocalAuthenticator_LocksOutAfterConfiguredFailures()
	{
		await using var harness = await CreateHarnessAsync();
		using var scope = harness.Provider.CreateScope();

		var authenticator = scope.ServiceProvider
			.GetRequiredService<IUserDirectory>()
			.GetLocalAuthenticator(harness.PrimaryRealm);

		// Both implementations default MaxFailedAccessAttempts to the same value; loop to that threshold instead of
		// a magic number so the test tracks the configured policy (and surfaces any fake/module parity drift).
		var maxFailedAttempts = new UserAccountsRealmOptions().PasswordOptions.MaxFailedAccessAttempts;
		for (var i = 0; i < maxFailedAttempts; i++)
		{
			var failed = await authenticator.AuthenticateLocalAsync("alice", $"wrong-{i}");
			Assert.Equal(AuthenticationFailureReason.InvalidCredentials, failed.Reason);
		}

		var locked = await authenticator.AuthenticateLocalAsync("alice", "alice");

		Assert.False(locked.Success);
		Assert.Equal(AuthenticationFailureReason.Blocked, locked.Reason);
	}

	// The shared contract only asserts claim-TYPE filtering, not scope-intersection: the in-memory fake
	// (MemoryUserClaimsProvider) ignores identityScopeNames and projects by claim type alone. Scope-intersection
	// is a module-specific guarantee and is covered against the real module in UserAccountsIntegrationTests.
	[Fact]
	public async Task ClaimsProvider_ProjectsSeededProfileEmailAndRoles()
	{
		await using var harness = await CreateHarnessAsync();
		using var scope = harness.Provider.CreateScope();

		var claims = await scope.ServiceProvider
			.GetRequiredService<IUserDirectory>()
			.GetClaimsProvider(harness.PrimaryRealm)
			.GetClaimsAsync(
				MemoryStorage.AliceSubjectId,
				["profile", "email"],
				[
					Constants.Jwt.ClaimTypes.PreferredUserName,
					JwtRegisteredClaimNames.Name,
					JwtRegisteredClaimNames.Email,
					Constants.Jwt.ClaimTypes.Role
				]);

		Assert.Contains(claims, c => c.Type == Constants.Jwt.ClaimTypes.PreferredUserName && c.Value == "alice");
		Assert.Contains(claims, c => c.Type == JwtRegisteredClaimNames.Name && c.Value == "Alice");
		Assert.Contains(claims, c => c.Type == JwtRegisteredClaimNames.Email && c.Value == "Alice@example.com");
		Assert.Contains(claims, c => c.Type == Constants.Jwt.ClaimTypes.Role && c.Value == "admin");
	}

	[Fact]
	public async Task ClaimsProvider_ReturnsNothingForInactiveAccounts()
	{
		await using var harness = await CreateHarnessAsync();
		await SeedAsync(harness, new UserSeed(
			harness.PrimaryRealm,
			"inactive-claims-subject",
			"inactive-claims",
			"Inactive Claims",
			"inactive-claims@example.com",
			"inactive-claims",
			IsActive: false,
			Roles: ["user"]));

		using var scope = harness.Provider.CreateScope();
		var claims = await scope.ServiceProvider
			.GetRequiredService<IUserDirectory>()
			.GetClaimsProvider(harness.PrimaryRealm)
			.GetClaimsAsync(
				"inactive-claims-subject",
				["profile", "email"],
				[
					Constants.Jwt.ClaimTypes.PreferredUserName,
					JwtRegisteredClaimNames.Name,
					JwtRegisteredClaimNames.Email,
					Constants.Jwt.ClaimTypes.Role
				]);

		Assert.Empty(claims);
	}

	protected abstract Task<DirectoryContractHarness> CreateHarnessAsync();

	protected abstract Task SeedAsync(DirectoryContractHarness harness, UserSeed seed);

	protected static UserAccountsRealmOptions ContractOptions()
		=> UserAccountsTestOptions.Relaxed(minimumLength: 1, allowProvidedSubjectId: true);

	protected sealed class DirectoryContractHarness(ServiceProvider provider, Realm primaryRealm, Realm otherRealm)
		: IAsyncDisposable
	{
		public ServiceProvider Provider { get; } = provider;

		public Realm PrimaryRealm { get; } = primaryRealm;

		public Realm OtherRealm { get; } = otherRealm;

		public ValueTask DisposeAsync() => Provider.DisposeAsync();
	}

	protected sealed record UserSeed(
		Realm Realm,
		string SubjectId,
		string Username,
		string DisplayName,
		string Email,
		string? Password,
		bool IsActive,
		IReadOnlyList<string> Roles);

	protected sealed class FakeClock(DateTimeOffset start) : TimeProvider
	{
		public DateTimeOffset Now { get; set; } = start;

		public override DateTimeOffset GetUtcNow() => Now;
	}

	public sealed class InMemory : UserDirectoryContractTests
	{
		protected override Task<DirectoryContractHarness> CreateHarnessAsync()
		{
			var services = new ServiceCollection();
			services.AddSingleton<TimeProvider>(new FakeClock(Start));
			services.AddTransient<IPasswordProtector, DefaultPasswordProtector>();
			services.AddInMemoryStorage();

			var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
			return Task.FromResult(new DirectoryContractHarness(
				provider,
				MemoryStorage.DemoRealm,
				MemoryStorage.AccountRealm));
		}

		protected override Task SeedAsync(DirectoryContractHarness harness, UserSeed seed)
		{
			var storage = harness.Provider.GetRequiredService<MemoryStorage>();
			storage.GetRealmMemoryStore(seed.Realm).UserAccounts[seed.Username] = new MemoryUserAccount
			{
				SubjectId = seed.SubjectId,
				Username = seed.Username,
				PasswordHash = seed.Password is null ? null : PasswordHash.Create(seed.Password),
				DisplayName = seed.DisplayName,
				IsActive = seed.IsActive,
				Roles = [.. seed.Roles],
				Claims = [new Claim(JwtRegisteredClaimNames.Email, seed.Email)]
			};
			return Task.CompletedTask;
		}
	}

	public sealed class UserAccountsSqlite : UserDirectoryContractTests
	{
		protected override async Task<DirectoryContractHarness> CreateHarnessAsync()
		{
			var options = ContractOptions();
			var services = new ServiceCollection();
			services.AddSingleton<TimeProvider>(new FakeClock(Start));
			services.AddTransient<IPasswordProtector, DefaultPasswordProtector>();
			services.AddSingleton<IUserAccountsRealmOptionsResolver>(
				new DefaultUserAccountsRealmOptionsResolver(options));
			services.AddUserAccountsSqliteInMemory();
			services.AddUserAccountsForRoyalIdentity();

			var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
			var harness = new DirectoryContractHarness(provider, MemoryStorage.DemoRealm, MemoryStorage.AccountRealm);

			using (var scope = harness.Provider.CreateScope())
			{
				var db = scope.ServiceProvider.GetRequiredService<UserAccountsDbContext>();
				await UserAccountsModuleSeed.SeedDefaultScopesAsync(db, harness.PrimaryRealm.Id, Start);
				await UserAccountsModuleSeed.SeedDefaultAccountsAsync(
					scope.ServiceProvider, harness.PrimaryRealm.Id, options, Start);
			}

			return harness;
		}

		protected override async Task SeedAsync(DirectoryContractHarness harness, UserSeed seed)
		{
			using var scope = harness.Provider.CreateScope();
			var options = scope.ServiceProvider
				.GetRequiredService<IUserAccountsRealmOptionsResolver>()
				.Resolve(seed.Realm.Id);

			await UserAccountsModuleSeed.SeedAccountAsync(
				scope.ServiceProvider, seed.Realm.Id, options, seed.SubjectId, seed.Username, seed.DisplayName,
				seed.Email, seed.Password, seed.IsActive, seed.Roles, Start);
		}
	}
}
