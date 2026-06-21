using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using RoyalIdentity.Contracts;
using RoyalIdentity.Events;
using RoyalIdentity.Models;
using RoyalIdentity.Models.Scopes;
using RoyalIdentity.Options;
using RoyalIdentity.UserAccounts.Features.Accounts.Domain;
using RoyalIdentity.UserAccounts.Features.Accounts.UseCases;
using RoyalIdentity.UserAccounts.Features.ScopeProperties.Domain;
using RoyalIdentity.UserAccounts.Features.ScopeProperties.UseCases;
using RoyalIdentity.UserAccounts.Integration;
using RoyalIdentity.UserAccounts.Options;
using RoyalIdentity.UserAccounts.Sqlite;
using RoyalIdentity.Users;
using RoyalIdentity.Users.Contracts;
using RoyalIdentity.Users.Contexts;
using RoyalIdentity.Users.Defaults;

namespace Tests.UserAccounts;

/// <summary>
/// Fase 9 (plan-users-accounts-module-v2): the <c>.Integration</c> adapter ports over the real module + SQLite
/// in-memory. Validates that <see cref="IUserDirectory"/> is realm-bound, that the subject store maps without
/// leaking the physical id, that the authenticator keeps a generic external error while preserving the internal
/// reason, and that the claims provider treats requested scopes/claim types as an intersection (never an implicit
/// grant) emitting BCL <see cref="Claim"/> only at the edge.
/// </summary>
public class UserAccountsIntegrationTests
{
	private static readonly DateTimeOffset Start = new(2026, 6, 20, 12, 0, 0, TimeSpan.Zero);

	// ---- SubjectStore ----

	[Fact]
	public async Task SubjectStore_ResolvesSubject_AndIsRealmScoped()
	{
		var options = Options();
		await using var provider = BuildProvider(options);
		await CreateAsync(provider, "r1", "alice", options, subjectId: "alice", displayName: "Alice Liddell");

		using var scope = provider.CreateScope();
		var directory = scope.ServiceProvider.GetRequiredService<IUserDirectory>();

		var subject = await directory.GetSubjectStore(Realm("r1")).FindBySubjectIdAsync("alice");
		Assert.NotNull(subject);
		Assert.Equal("alice", subject!.SubjectId);
		Assert.Equal("Alice Liddell", subject.DisplayName);
		Assert.True(subject.IsActive);
		Assert.True(await directory.GetSubjectStore(Realm("r1")).IsActiveAsync("alice"));

		// realm-bound: a store bound to another realm never sees this account.
		Assert.Null(await directory.GetSubjectStore(Realm("r2")).FindBySubjectIdAsync("alice"));
		Assert.False(await directory.GetSubjectStore(Realm("r2")).IsActiveAsync("alice"));
	}

	[Fact]
	public async Task SubjectStore_ReportsInactive_AfterDeactivation()
	{
		var options = Options();
		await using var provider = BuildProvider(options);
		await CreateAsync(provider, "r1", "alice", options, subjectId: "alice");
		await MutateAsync(provider, "r1", "alice", a => a.Deactivate(Start));

		using var scope = provider.CreateScope();
		var store = scope.ServiceProvider.GetRequiredService<IUserDirectory>().GetSubjectStore(Realm("r1"));

		var subject = await store.FindBySubjectIdAsync("alice");
		Assert.NotNull(subject);
		Assert.False(subject!.IsActive);
		Assert.False(await store.IsActiveAsync("alice"));
	}

	// ---- LocalUserAuthenticator ----

	[Fact]
	public async Task LocalAuthenticator_Succeeds_AndReturnsSubjectWithDisplayName()
	{
		var options = Options();
		await using var provider = BuildProvider(options);
		await CreateAsync(provider, "r1", "alice", options, subjectId: "alice", displayName: "Alice Liddell", password: "secret");

		var result = await AuthAsync(provider, "r1", "alice", "secret");

		Assert.True(result.Success);
		Assert.NotNull(result.Subject);
		Assert.Equal("alice", result.Subject!.SubjectId);
		Assert.Equal("Alice Liddell", result.Subject.DisplayName);
		Assert.True(result.Subject.IsActive);
		Assert.Null(result.Reason);
	}

	[Fact]
	public async Task LocalAuthenticator_MapsInternalReason_ToEdgeReason()
	{
		var options = Options();
		options.PasswordOptions.MaxFailedAccessAttempts = 2;
		options.PasswordOptions.AccountLockoutDurationMinutes = 10;
		await using var provider = BuildProvider(options);

		// not found
		Assert.Equal(AuthenticationFailureReason.NotFound, (await AuthAsync(provider, "r1", "ghost", "secret")).Reason);

		// password not set -> generic invalid credentials
		await CreateAsync(provider, "r1", "nopass", options, subjectId: "nopass", password: null);
		Assert.Equal(AuthenticationFailureReason.InvalidCredentials, (await AuthAsync(provider, "r1", "nopass", "secret")).Reason);

		await CreateAsync(provider, "r1", "alice", options, subjectId: "alice", password: "secret");

		// invalid credentials
		Assert.Equal(AuthenticationFailureReason.InvalidCredentials, (await AuthAsync(provider, "r1", "alice", "wrong")).Reason);

		// lockout (2nd failure locks) maps to Blocked
		Assert.Equal(AuthenticationFailureReason.InvalidCredentials, (await AuthAsync(provider, "r1", "alice", "wrong")).Reason);
		Assert.Equal(AuthenticationFailureReason.Blocked, (await AuthAsync(provider, "r1", "alice", "secret")).Reason);

		// inactive
		await MutateAsync(provider, "r1", "alice", a => a.Deactivate(Start));
		Assert.Equal(AuthenticationFailureReason.Inactive, (await AuthAsync(provider, "r1", "alice", "secret")).Reason);

		// administrative block
		await MutateAsync(provider, "r1", "alice", a =>
		{
			a.Activate(Start);
			a.UnlockLocalCredential(Start);
			a.Block("administrative", Start);
		});
		Assert.Equal(AuthenticationFailureReason.Blocked, (await AuthAsync(provider, "r1", "alice", "secret")).Reason);
	}

	[Fact]
	public async Task LocalAuthenticator_FailedResult_NeverCarriesSubject()
	{
		var options = Options();
		await using var provider = BuildProvider(options);

		var result = await AuthAsync(provider, "r1", "ghost", "secret");

		Assert.False(result.Success);
		Assert.Null(result.Subject);
		Assert.NotNull(result.Reason);
	}

	[Theory]
	[InlineData("inactive", AuthenticationFailureReason.Inactive)]
	[InlineData("blocked", AuthenticationFailureReason.Blocked)]
	public async Task LoginFlow_KeepsGenericExternalMessage_AndPreservesInternalReason(
		string accountState,
		AuthenticationFailureReason expectedReason)
	{
		var options = Options();
		await using var provider = BuildProvider(options);
		await CreateAsync(provider, "r1", "alice", options, subjectId: "alice", password: "secret");
		await MutateAsync(provider, "r1", "alice", account =>
		{
			if (accountState == "inactive")
			{
				account.Deactivate(Start);
			}
			else
			{
				account.Block("administrative", Start);
			}
		});

		var realm = Realm("r1");
		var events = new CapturingEventDispatcher();

		using var scope = provider.CreateScope();
		var flow = new LoginFlowService(
			scope.ServiceProvider.GetRequiredService<IUserDirectory>(),
			new ThrowingUserSessionService(),
			new ThrowingSubjectPrincipalFactory(),
			new ThrowingConsentService(),
			new NullAuthorizationContextResolver(),
			new FixedCurrentRealmAccessor(realm),
			events,
			new HttpContextAccessor(),
			NullLogger<LoginFlowService>.Instance);

		var result = await flow.LoginAsync(new LoginRequest("alice", "secret", ReturnUrl: null, RememberLogin: false), default);

		Assert.Equal(LoginFlowOutcome.Error, result.Outcome);
		Assert.Equal(realm.Options.Account.InvalidCredentialsErrorMessage, result.ErrorMessage);

		var evt = Assert.IsType<UserLoginFailureEvent>(Assert.Single(events.Events));
		Assert.Equal(expectedReason, evt.Reason);
		Assert.Equal(realm.Options.Account.InvalidCredentialsErrorMessage, evt.Message);
		Assert.Equal("r1", evt.RealmId);
	}

	// ---- UserClaimsProvider ----

	[Fact]
	public async Task ClaimsProvider_CombinesFixedFields_Roles_AndDynamicValues_AsBclClaims()
	{
		var options = Options();
		await using var provider = BuildProvider(options);
		await CreateAsync(provider, "r1", "alice", options, subjectId: "alice",
			displayName: "Alice Liddell", email: "alice@example.com", emailVerified: true, roles: ["admin"]);
		await SeedScopeAsync(provider, "r1", "profile", "nickname");
		await SetPropertyAsync(provider, "r1", "alice", "profile", "nickname", ["ally"]);

		var claims = await ClaimsAsync(provider, "r1", "alice",
			["profile", "email"],
			["preferred_username", "name", "role", "email", "email_verified", "nickname"]);

		Assert.Contains(claims, c => c.Type == "preferred_username" && c.Value == "alice");
		Assert.Contains(claims, c => c.Type == "name" && c.Value == "Alice Liddell");
		Assert.Contains(claims, c => c.Type == "role" && c.Value == "admin");
		Assert.Contains(claims, c => c.Type == "email" && c.Value == "alice@example.com");
		Assert.Contains(claims, c => c.Type == "email_verified" && c.Value == "true");
		Assert.Contains(claims, c => c.Type == "nickname" && c.Value == "ally");
	}

	[Fact]
	public async Task ClaimsProvider_DoesNotEmit_ClaimNotRequestedByIdp()
	{
		var options = Options();
		await using var provider = BuildProvider(options);
		await CreateAsync(provider, "r1", "alice", options, subjectId: "alice", roles: ["admin"]);
		await SeedScopeAsync(provider, "r1", "profile", "nickname");
		await SetPropertyAsync(provider, "r1", "alice", "profile", "nickname", ["ally"]);

		// the scope is requested, but neither 'nickname' nor 'role' claim types are.
		var claims = await ClaimsAsync(provider, "r1", "alice", ["profile"], ["preferred_username"]);

		Assert.Contains(claims, c => c.Type == "preferred_username");
		Assert.DoesNotContain(claims, c => c.Type == "nickname");
		Assert.DoesNotContain(claims, c => c.Type == "role");
	}

	[Fact]
	public async Task ClaimsProvider_DoesNotEmit_ClaimRequestedButAbsentInModule()
	{
		var options = Options();
		await using var provider = BuildProvider(options);
		await CreateAsync(provider, "r1", "alice", options, subjectId: "alice");

		// 'nickname' is authorized by the IdP request but the account has no such value.
		var claims = await ClaimsAsync(provider, "r1", "alice", ["profile"], ["nickname", "preferred_username"]);

		Assert.DoesNotContain(claims, c => c.Type == "nickname");
		Assert.Contains(claims, c => c.Type == "preferred_username");
	}

	[Fact]
	public async Task ClaimsProvider_ReturnsNothing_ForInactiveAccount()
	{
		var options = Options();
		await using var provider = BuildProvider(options);
		await CreateAsync(provider, "r1", "alice", options, subjectId: "alice");
		await MutateAsync(provider, "r1", "alice", a => a.Deactivate(Start));

		var claims = await ClaimsAsync(provider, "r1", "alice", ["profile"], ["preferred_username"]);

		Assert.Empty(claims);
	}

	[Fact]
	public void AddUserAccountsForRoyalIdentity_AllowsScopedRealmOptionsResolver()
	{
		var services = new ServiceCollection();
		services.AddScoped<IUserAccountsRealmOptionsResolver, ScopedRealmOptionsResolver>();
		services.AddUserAccountsForRoyalIdentity();

		using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
		using var scope = provider.CreateScope();

		var binding = scope.ServiceProvider
			.GetRequiredService<UserAccountsRealmBindingFactory>()
			.Create(Realm("r1"));

		Assert.Equal("r1", binding.RealmId);
	}

	// ---- harness ----

	private static ServiceProvider BuildProvider(UserAccountsRealmOptions resolverOptions)
	{
		var services = new ServiceCollection();
		services.AddSingleton<TimeProvider>(new FakeClock(Start));
		services.AddTransient<IPasswordProtector, DefaultPasswordProtector>();

		// Registered before the integration so its TryAdd keeps this realm options resolver (drives the ports).
		services.AddSingleton<IUserAccountsRealmOptionsResolver>(
			new DefaultUserAccountsRealmOptionsResolver(resolverOptions));

		services.AddUserAccountsSqliteInMemory();
		services.AddUserAccountsForRoyalIdentity();
		return services.BuildServiceProvider();
	}

	private static Realm Realm(string id)
		=> new(id, $"{id}.example", id, id, @internal: false, new RealmOptions(new ServerOptions()));

	private static UserAccountsRealmOptions Options()
		=> UserAccountsTestOptions.Relaxed(allowProvidedSubjectId: true);

	private static async Task CreateAsync(
		ServiceProvider provider,
		string realmId,
		string username,
		UserAccountsRealmOptions options,
		string? subjectId = null,
		string? displayName = null,
		string? email = null,
		bool emailVerified = false,
		string? password = "secret",
		IReadOnlyList<string>? roles = null)
	{
		using var scope = provider.CreateScope();
		var handler = scope.ServiceProvider.GetRequiredService<ICreateUserAccountHandler>();
		var result = await handler.HandleAsync(new CreateUserAccount
		{
			RealmId = realmId,
			Options = options,
			Username = username,
			DisplayName = displayName,
			Email = email,
			EmailVerified = emailVerified,
			Password = password,
			SubjectId = subjectId,
			Roles = roles
		}, default);

		Assert.True(result.IsSuccess);
	}

	private static async Task<AuthenticationResult> AuthAsync(
		ServiceProvider provider, string realmId, string login, string password)
	{
		using var scope = provider.CreateScope();
		var directory = scope.ServiceProvider.GetRequiredService<IUserDirectory>();
		return await directory.GetLocalAuthenticator(Realm(realmId)).AuthenticateLocalAsync(login, password);
	}

	private static async Task<IReadOnlyList<Claim>> ClaimsAsync(
		ServiceProvider provider,
		string realmId,
		string subjectId,
		IReadOnlyCollection<string> scopeNames,
		IReadOnlyCollection<string> claimTypes)
	{
		using var scope = provider.CreateScope();
		var directory = scope.ServiceProvider.GetRequiredService<IUserDirectory>();
		return await directory.GetClaimsProvider(Realm(realmId)).GetClaimsAsync(subjectId, scopeNames, claimTypes);
	}

	private static async Task SetPropertyAsync(
		ServiceProvider provider, string realmId, string subjectId, string scopeName, string claimType, IReadOnlyList<string> values)
	{
		using var scope = provider.CreateScope();
		var handler = scope.ServiceProvider.GetRequiredService<ISetUserAccountScopePropertyHandler>();
		var result = await handler.HandleAsync(new SetUserAccountScopeProperty
		{
			RealmId = realmId,
			SubjectId = subjectId,
			ScopeName = scopeName,
			ClaimType = claimType,
			Values = values
		}, default);

		Assert.True(result.IsSuccess);
	}

	private static async Task SeedScopeAsync(
		ServiceProvider provider, string realmId, string scopeName, string claimType)
	{
		using var scope = provider.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<UserAccountsSqliteDbContext>();
		var propertyScope = new PropertyScope(realmId, scopeName, scopeName, Start);
		var version = propertyScope.Versions.Single();
		Assert.True(propertyScope.AddDefinition(version, claimType, new PropertyDefinitionSettings
		{
			ValueType = PropertyValueType.Text,
			IsActive = true
		}).IsSuccess);
		Assert.True(propertyScope.ApproveVersion(version, Start).IsSuccess);
		db.PropertyScopes.Add(propertyScope);
		await db.SaveChangesAsync();
	}

	private static async Task MutateAsync(
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

	private sealed class ScopedRealmOptionsResolver : IUserAccountsRealmOptionsResolver
	{
		public UserAccountsRealmOptions Resolve(string realmId) => Options();
	}

	private sealed class NullAuthorizationContextResolver : IAuthorizationContextResolver
	{
		public Task<AuthorizationContext?> ResolveAsync(string? returnUrl, CancellationToken ct = default)
			=> Task.FromResult<AuthorizationContext?>(null);
	}

	private sealed class FixedCurrentRealmAccessor(Realm realm) : ICurrentRealmAccessor
	{
		public Realm GetCurrentRealm() => realm;

		public bool TryGetCurrentRealm([NotNullWhen(true)] out Realm? current)
		{
			current = realm;
			return true;
		}
	}

	private sealed class CapturingEventDispatcher : IEventDispatcher
	{
		public List<Event> Events { get; } = [];

		public ValueTask DispatchAsync(Event evt)
		{
			Events.Add(evt);
			return ValueTask.CompletedTask;
		}

		public ValueTask DispatchAsync(Event evt, Realm realm)
		{
			evt.RealmId = realm.Id;
			Events.Add(evt);
			return ValueTask.CompletedTask;
		}
	}

	private sealed class ThrowingUserSessionService : IUserSessionService
	{
		public Task<UserSession?> GetCurrentAsync(ClaimsPrincipal principal, CancellationToken ct = default)
			=> throw new NotSupportedException();

		public Task<bool> IsSessionValidAsync(ClaimsPrincipal principal, CancellationToken ct = default)
			=> throw new NotSupportedException();

		public Task<UserSession> StartAsync(
			Subject subject, string authenticationMethod, string identityProvider, CancellationToken ct = default)
			=> throw new NotSupportedException();

		public Task<UserSession?> EndAsync(string sessionId, CancellationToken ct = default)
			=> throw new NotSupportedException();

		public Task RecordClientAsync(string sessionId, string clientId, CancellationToken ct = default)
			=> throw new NotSupportedException();
	}

	private sealed class ThrowingSubjectPrincipalFactory : ISubjectPrincipalFactory
	{
		public ClaimsPrincipal Create(Subject subject, UserSession session) => throw new NotSupportedException();
	}

	private sealed class ThrowingConsentService : IConsentService
	{
		public ValueTask<bool> RequiresConsentAsync(
			ClaimsPrincipal subject, Client client, RequestedResources resources, CancellationToken ct)
			=> throw new NotSupportedException();

		public Task UpdateConsentAsync(
			ClaimsPrincipal subject, Client client, IEnumerable<ConsentedScope> scopes, CancellationToken ct)
			=> throw new NotSupportedException();

		public ValueTask<bool> ValidateConsentAsync(
			ClaimsPrincipal subject, Client client, RequestedResources resources, CancellationToken ct)
			=> throw new NotSupportedException();
	}
}
