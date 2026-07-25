using System.Reflection;
using Microsoft.Extensions.Logging.Abstractions;
using RoyalIdentity.Contracts.Defaults;
using RoyalIdentity.Contracts.Storage;
using RoyalIdentity.Models;
using RoyalIdentity.Models.Tokens;
using RoyalIdentity.Options;
using RoyalIdentity.Storage.EntityFramework.Operational.Stores;
using RoyalIdentity.Storage.InMemory;
using RoyalIdentity.Users.Contracts;
using Tests.Storage.Operational.Support;

namespace Tests.Storage.Operational;

/// <summary>
/// Shape and behavior of the public contracts introduced by Fase 1 of plan-data-operational-storage: the
/// atomic capabilities MP-2/MP-3 (DF11/DF12), their transitional detection and fallback (DF39), and the
/// realm-bound authorize-parameters accessor (MP-5/DF16). The capabilities must never carry a non-atomic
/// default implementation, the in-memory fake must never claim them (ADR-018/DF25), and only the core may
/// take the legacy path — explicitly.
/// </summary>
public class OperationalContractsShapeTests
{
	private static readonly Realm Realm =
		new("realm-a", "realm-a.test", "realm-a", "Realm A", false, new RealmOptions(new ServerOptions()));

	// DF39: a capability with a default implementation would let a backing appear atomic without being it.
	[Theory]
	[InlineData(typeof(ISingleUseAuthorizationCodeStore))]
	[InlineData(typeof(IVersionedRefreshTokenStore))]
	public void Capabilities_DeclareNoDefaultImplementation(Type capability)
	{
		var members = capability.GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

		Assert.NotEmpty(members);
		Assert.All(members, member => Assert.True(member is MethodInfo { IsAbstract: true }));
	}

	// The capabilities are separate interfaces: the CRUD contracts stay exactly as they were.
	[Fact]
	public void CrudContracts_DoNotDeclareTheAtomicOperations()
	{
		Assert.False(typeof(IAuthorizationCodeStore).IsAssignableTo(typeof(ISingleUseAuthorizationCodeStore)));
		Assert.False(typeof(IRefreshTokenStore).IsAssignableTo(typeof(IVersionedRefreshTokenStore)));

		Assert.DoesNotContain(
			typeof(IAuthorizationCodeStore).GetMethods(),
			method => method.Name.Contains("Consume", StringComparison.Ordinal));
		Assert.DoesNotContain(
			typeof(IRefreshTokenStore).GetMethods(),
			method => method.Name.StartsWith("Try", StringComparison.Ordinal));
	}

	// ADR-018/DF25: the fake stays transitional. It never gains the atomic capabilities, so the acceptances
	// that prove atomicity can only run against the EF provider.
	[Fact]
	public void InMemoryStores_DoNotImplementTheCapabilities()
	{
		Assert.False(typeof(AuthorizationCodeStore).IsAssignableTo(typeof(ISingleUseAuthorizationCodeStore)));
		Assert.False(typeof(RefreshTokenStore).IsAssignableTo(typeof(IVersionedRefreshTokenStore)));
	}

	// DF39: the EF composition must be unable to produce a store without the capability. The factory's own
	// return types carry it, so the guarantee is enforced by the compiler and the transitional fallback is
	// unreachable from this adapter by construction — no runtime check can be forgotten.
	[Fact]
	public void OperationalStoreFactory_ReturnsStoresThatCarryTheCapabilities()
	{
		var codeStore = typeof(IOperationalStoreFactory)
			.GetMethod(nameof(IOperationalStoreFactory.GetAuthorizationCodeStore))!.ReturnType;
		var refreshStore = typeof(IOperationalStoreFactory)
			.GetMethod(nameof(IOperationalStoreFactory.GetRefreshTokenStore))!.ReturnType;

		Assert.True(codeStore.IsAssignableTo(typeof(IAuthorizationCodeStore)));
		Assert.True(codeStore.IsAssignableTo(typeof(ISingleUseAuthorizationCodeStore)));
		Assert.True(refreshStore.IsAssignableTo(typeof(IRefreshTokenStore)));
		Assert.True(refreshStore.IsAssignableTo(typeof(IVersionedRefreshTokenStore)));
	}

	// The consumers must therefore take the capability path for anything the EF factory produces.
	[Fact]
	public async Task Consumers_TakeTheCapabilityPath_ForEveryStoreTheEfFactoryCanProduce()
	{
		var code = OperationalTestData.NewAuthorizationCode();
		var codeStore = new CapableAuthorizationCodeStore(code);
		var refreshStore = new CapableRefreshTokenStore(OperationalTestData.NewRefreshToken());
		var storage = new SingleStoreStorage { AuthorizationCodes = codeStore, RefreshTokens = refreshStore };

		// Both fakes satisfy the factory's return types, which is what the EF stores will have to satisfy too.
		Assert.True(codeStore is IOperationalAuthorizationCodeStore);
		Assert.True(refreshStore is IOperationalRefreshTokenStore);

		await new DefaultAuthorizationCodeConsumer(storage, NullLogger<DefaultAuthorizationCodeConsumer>.Instance)
			.ConsumeAsync(Realm, code.Code, code.ClientId, code.RedirectUri);

		Assert.Equal(1, codeStore.ConsumeCalls);
		Assert.Equal(0, codeStore.GetCalls);
	}

	// MP-5: authorize parameters are reached through a realm accessor, never through global state.
	[Fact]
	public void Storage_ExposesAuthorizeParameters_OnlyThroughARealmAccessor()
	{
		Assert.Null(typeof(IStorage).GetProperty("AuthorizeParameters"));

		var accessor = typeof(IStorage).GetMethod(nameof(IStorage.GetAuthorizeParametersStore));

		Assert.NotNull(accessor);
		Assert.Equal(typeof(IAuthorizeParametersStore), accessor!.ReturnType);
		Assert.Equal(typeof(Realm), Assert.Single(accessor.GetParameters()).ParameterType);
	}

	// DF11: with the capability present, the consumer delegates to the atomic primitive and never touches the
	// legacy get/remove pair.
	[Fact]
	public async Task AuthorizationCodeConsumer_UsesTheCapability_WhenTheBackingProvidesIt()
	{
		var code = OperationalTestData.NewAuthorizationCode();
		var store = new CapableAuthorizationCodeStore(code);
		var consumer = new DefaultAuthorizationCodeConsumer(
			new SingleStoreStorage { AuthorizationCodes = store },
			NullLogger<DefaultAuthorizationCodeConsumer>.Instance);

		var consumed = await consumer.ConsumeAsync(Realm, code.Code, code.ClientId, code.RedirectUri);

		Assert.Same(code, consumed);
		Assert.Equal(1, store.ConsumeCalls);
		Assert.Equal(0, store.GetCalls);
		Assert.Equal(0, store.RemoveCalls);
	}

	// DF39: without the capability the consumer takes the legacy path explicitly — and still exposes the
	// target semantics, so the flow does not change when the backing is swapped in Plano 4.
	[Fact]
	public async Task AuthorizationCodeConsumer_FallsBackToGetAndRemove_WhenTheCapabilityIsAbsent()
	{
		var code = OperationalTestData.NewAuthorizationCode();
		var store = new LegacyAuthorizationCodeStore(code);
		var consumer = new DefaultAuthorizationCodeConsumer(
			new SingleStoreStorage { AuthorizationCodes = store },
			NullLogger<DefaultAuthorizationCodeConsumer>.Instance);

		var consumed = await consumer.ConsumeAsync(Realm, code.Code, code.ClientId, code.RedirectUri);

		Assert.Same(code, consumed);
		Assert.Equal(1, store.GetCalls);
		Assert.Equal(1, store.RemoveCalls);
	}

	// DF11: a mismatched binding returns the same null as an absent code — and does not consume it.
	[Theory]
	[InlineData("other-client", "https://client.example/callback")]
	[InlineData("client-one", "https://attacker.example/callback")]
	public async Task AuthorizationCodeConsumer_WithAMismatchedBinding_ReturnsNullWithoutConsuming(
		string clientId, string redirectUri)
	{
		var code = OperationalTestData.NewAuthorizationCode();
		var store = new LegacyAuthorizationCodeStore(code);
		var consumer = new DefaultAuthorizationCodeConsumer(
			new SingleStoreStorage { AuthorizationCodes = store },
			NullLogger<DefaultAuthorizationCodeConsumer>.Instance);

		var consumed = await consumer.ConsumeAsync(Realm, code.Code, clientId, redirectUri);

		Assert.Null(consumed);
		Assert.Equal(0, store.RemoveCalls);
	}

	// DF12: with the capability present, the consumer passes the version obtained from materialization — not
	// the state of the instance it is about to write.
	[Fact]
	public async Task RefreshTokenConsumer_UsesTheCapability_WithTheMaterializedVersion()
	{
		var token = OperationalTestData.NewRefreshToken();
		token.StateVersion = 7;
		var store = new CapableRefreshTokenStore(token);
		var consumer = new DefaultRefreshTokenConsumer(
			new SingleStoreStorage { RefreshTokens = store },
			NullLogger<DefaultRefreshTokenConsumer>.Instance);

		var transition = await consumer.TryConsumeAsync(Realm, token, OperationalTestData.CreationTime);

		Assert.True(transition.IsSuccess);
		Assert.Equal(7, store.LastExpectedVersion);
		Assert.Equal(0, store.UpdateCalls);
	}

	// DF12: a conflict is never converted into a success, and it reports the rematerialized state so the
	// caller — not this seam — can decide about the tolerance policy.
	[Fact]
	public async Task RefreshTokenConsumer_ReportsAConflictWithTheRematerializedState()
	{
		var token = OperationalTestData.NewRefreshToken();
		var rematerialized = OperationalTestData.NewRefreshToken();
		rematerialized.ConsumedTime = OperationalTestData.CreationTime;
		rematerialized.StateVersion = 1;

		var store = new CapableRefreshTokenStore(token)
		{
			Result = RefreshTokenTransition.Conflict(rematerialized),
		};
		var consumer = new DefaultRefreshTokenConsumer(
			new SingleStoreStorage { RefreshTokens = store },
			NullLogger<DefaultRefreshTokenConsumer>.Instance);

		var transition = await consumer.TryConsumeAsync(Realm, token, OperationalTestData.CreationTime);

		Assert.False(transition.IsSuccess);
		Assert.Equal(RefreshTokenTransitionOutcome.Conflict, transition.Outcome);
		Assert.Same(rematerialized, transition.Current);
		Assert.Null(token.ConsumedTime);
	}

	// DF39: without the capability, the legacy update runs and the seam says so through the same result type.
	[Fact]
	public async Task RefreshTokenConsumer_FallsBackToTheLegacyUpdate_WhenTheCapabilityIsAbsent()
	{
		var token = OperationalTestData.NewRefreshToken();
		var store = new LegacyRefreshTokenStore();
		var consumer = new DefaultRefreshTokenConsumer(
			new SingleStoreStorage { RefreshTokens = store },
			NullLogger<DefaultRefreshTokenConsumer>.Instance);

		var transition = await consumer.TryConsumeAsync(Realm, token, OperationalTestData.CreationTime);

		Assert.True(transition.IsSuccess);
		Assert.Equal(1, store.UpdateCalls);
		Assert.Equal(OperationalTestData.CreationTime, token.ConsumedTime);
	}

	// Even on the legacy path, an already consumed token is reported as such instead of being consumed twice.
	[Fact]
	public async Task RefreshTokenConsumer_OnTheLegacyPath_ReportsAnAlreadyConsumedToken()
	{
		var token = OperationalTestData.NewRefreshToken();
		token.ConsumedTime = OperationalTestData.CreationTime;
		var store = new LegacyRefreshTokenStore();
		var consumer = new DefaultRefreshTokenConsumer(
			new SingleStoreStorage { RefreshTokens = store },
			NullLogger<DefaultRefreshTokenConsumer>.Instance);

		var transition = await consumer.TryConsumeAsync(Realm, token, OperationalTestData.CreationTime.AddMinutes(1));

		Assert.Equal(RefreshTokenTransitionOutcome.AlreadyConsumed, transition.Outcome);
		Assert.Equal(0, store.UpdateCalls);
		Assert.Equal(OperationalTestData.CreationTime, token.ConsumedTime);
	}

	// Implements the very interface the EF factory must return, so this stand-in exercises the same shape the
	// real store will have.
	private sealed class CapableAuthorizationCodeStore(AuthorizationCode code) : IOperationalAuthorizationCodeStore
	{
		public int ConsumeCalls { get; private set; }

		public int GetCalls { get; private set; }

		public int RemoveCalls { get; private set; }

		public Task<AuthorizationCode?> ConsumeAuthorizationCodeAsync(
			string handle, string clientId, string redirectUri, CancellationToken ct)
		{
			ConsumeCalls++;
			return Task.FromResult<AuthorizationCode?>(code);
		}

		public Task<AuthorizationCode?> GetAuthorizationCodeAsync(string handle, CancellationToken ct)
		{
			GetCalls++;
			return Task.FromResult<AuthorizationCode?>(code);
		}

		public Task RemoveAuthorizationCodeAsync(string handle, CancellationToken ct)
		{
			RemoveCalls++;
			return Task.CompletedTask;
		}

		public Task<string> StoreAuthorizationCodeAsync(AuthorizationCode authorizationCode, CancellationToken ct)
			=> Task.FromResult(authorizationCode.Code);
	}

	private sealed class LegacyAuthorizationCodeStore(AuthorizationCode code) : IAuthorizationCodeStore
	{
		public int GetCalls { get; private set; }

		public int RemoveCalls { get; private set; }

		public Task<AuthorizationCode?> GetAuthorizationCodeAsync(string handle, CancellationToken ct)
		{
			GetCalls++;
			return Task.FromResult<AuthorizationCode?>(
				string.Equals(handle, code.Code, StringComparison.Ordinal) ? code : null);
		}

		public Task RemoveAuthorizationCodeAsync(string handle, CancellationToken ct)
		{
			RemoveCalls++;
			return Task.CompletedTask;
		}

		public Task<string> StoreAuthorizationCodeAsync(AuthorizationCode authorizationCode, CancellationToken ct)
			=> Task.FromResult(authorizationCode.Code);
	}

	/// <inheritdoc cref="CapableAuthorizationCodeStore"/>
	private sealed class CapableRefreshTokenStore(RefreshToken token) : IOperationalRefreshTokenStore
	{
		public RefreshTokenTransition? Result { get; set; }

		public int? LastExpectedVersion { get; private set; }

		public int UpdateCalls { get; private set; }

		public Task<RefreshTokenTransition> TryConsumeAsync(
			string handle, int expectedStateVersion, DateTime consumedAt, CancellationToken ct)
		{
			LastExpectedVersion = expectedStateVersion;
			return Task.FromResult(Result ?? RefreshTokenTransition.Succeeded(token));
		}

		public Task<RefreshTokenTransition> TryUpdateAsync(
			RefreshToken refreshToken, int expectedStateVersion, CancellationToken ct)
		{
			LastExpectedVersion = expectedStateVersion;
			return Task.FromResult(Result ?? RefreshTokenTransition.Succeeded(refreshToken));
		}

		public Task<RefreshToken?> GetAsync(string handle, CancellationToken ct)
			=> Task.FromResult<RefreshToken?>(token);

		public Task RemoveAsync(string handle, CancellationToken ct) => Task.CompletedTask;

		public Task<int> RemoveBySubjectAsync(string subjectId, CancellationToken ct) => Task.FromResult(0);

		public Task StoreAsync(RefreshToken refreshToken, CancellationToken ct) => Task.CompletedTask;

		public Task UpdateAsync(RefreshToken refreshToken, CancellationToken ct)
		{
			UpdateCalls++;
			return Task.CompletedTask;
		}
	}

	private sealed class LegacyRefreshTokenStore : IRefreshTokenStore
	{
		public int UpdateCalls { get; private set; }

		public Task<RefreshToken?> GetAsync(string handle, CancellationToken ct)
			=> Task.FromResult<RefreshToken?>(null);

		public Task RemoveAsync(string handle, CancellationToken ct) => Task.CompletedTask;

		public Task<int> RemoveBySubjectAsync(string subjectId, CancellationToken ct) => Task.FromResult(0);

		public Task StoreAsync(RefreshToken refreshToken, CancellationToken ct) => Task.CompletedTask;

		public Task UpdateAsync(RefreshToken refreshToken, CancellationToken ct)
		{
			UpdateCalls++;
			return Task.CompletedTask;
		}
	}

	/// <summary>
	/// Minimal <see cref="IStorage"/> exposing only the two stores the capability seams reach; every other
	/// member would be a scenario this test does not describe.
	/// </summary>
	private sealed class SingleStoreStorage : IStorage
	{
		public IAuthorizationCodeStore? AuthorizationCodes { get; init; }

		public IRefreshTokenStore? RefreshTokens { get; init; }

		public IAuthorizationCodeStore GetAuthorizationCodeStore(Realm realm)
			=> AuthorizationCodes ?? throw new NotSupportedException();

		public IRefreshTokenStore GetRefreshTokenStore(Realm realm)
			=> RefreshTokens ?? throw new NotSupportedException();

		public ServerOptions ServerOptions => throw new NotSupportedException();

		public IRealmStore Realms => throw new NotSupportedException();

		public IAuthorizeParametersStore GetAuthorizeParametersStore(Realm realm) => throw new NotSupportedException();

		public IAccessTokenStore GetAccessTokenStore(Realm realm) => throw new NotSupportedException();

		public IUserConsentStore GetUserConsentStore(Realm realm) => throw new NotSupportedException();

		public IKeyStore GetKeyStore(Realm realm) => throw new NotSupportedException();

		public IClientStore GetClientStore(Realm realm) => throw new NotSupportedException();

		public IResourceStore GetResourceStore(Realm realm) => throw new NotSupportedException();

		public IUserSessionStore GetUserSessionStore(Realm realm) => throw new NotSupportedException();
	}
}
