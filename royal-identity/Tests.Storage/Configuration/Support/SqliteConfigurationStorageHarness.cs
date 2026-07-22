using System.Collections.Concurrent;
using System.Collections.Specialized;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RoyalIdentity.Contracts.Storage;
using RoyalIdentity.Data.Configuration.Entities;
using RoyalIdentity.Models;
using RoyalIdentity.Models.Scopes;
using RoyalIdentity.Models.Tokens;
using RoyalIdentity.Options;
using RoyalIdentity.Storage.EntityFramework.Configuration.Materialization;
using RoyalIdentity.Storage.EntityFramework.Configuration.Resources;
using RoyalIdentity.Storage.EntityFramework.Configuration.Stores;
using RoyalIdentity.Storage.EntityFramework.Extensions;
using RoyalIdentity.Storage.EntityFramework.Security.KeyMaterial;
using RoyalIdentity.Storage.EntityFramework.Sqlite;
using RoyalIdentity.Storage.InMemory;
using RoyalIdentity.Users;
using RoyalIdentity.Users.Contracts;
using Tests.Storage.Support;

namespace Tests.Storage.Configuration.Support;

/// <summary>
/// Configuration-EF fixture for the provider-neutral contracts. Only Configuration operations use SQLite;
/// Operational members are isolated in test-local dictionaries so no partial production <see cref="IStorage"/>
/// is registered (plan DF20).
/// </summary>
internal sealed class SqliteConfigurationStorageHarness : StorageContractHarness
{
	private readonly SqliteConfigurationDatabase database;
	private readonly ServiceProvider services;
	private readonly AsyncServiceScope scope;
	private readonly MutableConfigurationResourceSource resourceSource;
	private readonly ClientMaterializer clientMaterializer;
	private Realm realmA = null!;
	private Realm realmB = null!;
	private Realm internalRealm = null!;

	private SqliteConfigurationStorageHarness(
		SqliteConfigurationDatabase database,
		ServiceProvider services,
		AsyncServiceScope scope,
		MutableConfigurationResourceSource resourceSource,
		ClientMaterializer clientMaterializer,
		ConfigurationCompositeStorage storage,
		FakeClock clock)
	{
		this.database = database;
		this.services = services;
		this.scope = scope;
		this.resourceSource = resourceSource;
		this.clientMaterializer = clientMaterializer;
		Storage = storage;
		Provider = new TestStorageProvider(storage);
		Clock = clock;
	}

	public override IStorage Storage { get; }

	public override IStorageProvider Provider { get; }

	public override FakeClock Clock { get; }

	public override Realm RealmA => realmA;

	public override Realm RealmB => realmB;

	public override Realm InternalRealm => internalRealm;

	internal ConfigurationSqliteDbContext DbContext
		=> scope.ServiceProvider.GetRequiredService<ConfigurationSqliteDbContext>();

	internal IConfigurationStoreFactory ConfigurationStores
		=> scope.ServiceProvider.GetRequiredService<IConfigurationStoreFactory>();

	public static async Task<StorageContractHarness> CreateAsync()
		=> await CreateConcreteAsync();

	internal static async Task<SqliteConfigurationStorageHarness> CreateConcreteAsync()
	{
		var database = await SqliteConfigurationDatabase.CreateMigratedAsync();
		var clock = new FakeClock(Start);
		var resourceSource = new MutableConfigurationResourceSource();
		var collection = new ServiceCollection();
		collection.AddSingleton<TimeProvider>(clock);
		collection.AddSingleton<IConfigurationResourceSource>(resourceSource);
		collection.AddDbContext<ConfigurationSqliteDbContext>(options => options.UseSqlite(database.Connection));
		collection.AddEntityFrameworkConfigurationStorage<ConfigurationSqliteDbContext>();
		collection.AddAesKeyMaterialProtector(options => options.Key = TestProtectorKey.ToArray());

		var services = collection.BuildServiceProvider(new ServiceProviderOptions
		{
			ValidateOnBuild = true,
			ValidateScopes = true,
		});
		var scope = services.CreateAsyncScope();

		try
		{
			var serverSerializer = scope.ServiceProvider.GetRequiredService<ServerOptionsPayloadSerializer>();
			var realmSerializer = scope.ServiceProvider.GetRequiredService<RealmOptionsPayloadSerializer>();
			var clientMaterializer = scope.ServiceProvider.GetRequiredService<ClientMaterializer>();
			var context = scope.ServiceProvider.GetRequiredService<ConfigurationSqliteDbContext>();
			var serverOptions = new ServerOptions();
			var serverPayload = serverSerializer.Serialize(serverOptions);
			context.ServerOptions.Add(new ServerOptionsEntity
			{
				Id = ServerOptionsEntity.SingletonId,
				PayloadVersion = serverPayload.Version,
				PayloadJson = serverPayload.Json,
				UpdatedAtUtc = Start,
			});

			var internalRealm = new Realm(
				"contract-internal",
				"internal.contract.test",
				"contract-internal",
				"Contract Internal Realm",
				true,
				new RealmOptions(serverOptions));
			var realmPayload = realmSerializer.Serialize(internalRealm.Options);
			context.Realms.Add(ToEntity(internalRealm, realmPayload));
			await context.SaveChangesAsync();

			var factory = scope.ServiceProvider.GetRequiredService<IConfigurationStoreFactory>();
			var storage = new ConfigurationCompositeStorage(factory, serverOptions, clock);
			storage.EnsureRealm(internalRealm.Id);

			var harness = new SqliteConfigurationStorageHarness(
				database,
				services,
				scope,
				resourceSource,
				clientMaterializer,
				storage,
				clock);

			harness.internalRealm = internalRealm;
			harness.realmA = await harness.CreateRealmAsync("a");
			harness.realmB = await harness.CreateRealmAsync("b");
			return harness;
		}
		catch
		{
			await scope.DisposeAsync();
			await services.DisposeAsync();
			await database.DisposeAsync();
			throw;
		}
	}

	public override async Task SeedClientAsync(Client client)
	{
		var entities = clientMaterializer.ToEntitySet(client);
		DbContext.Clients.Add(entities.Root);
		DbContext.ClientStringValues.AddRange(entities.StringValues);
		DbContext.ClientClaims.AddRange(entities.Claims);
		DbContext.ClientSecrets.AddRange(entities.Secrets);
		await DbContext.SaveChangesAsync();
	}

	public override Task SeedIdentityScopeAsync(Realm realm, IdentityScope identityScope)
	{
		resourceSource.AddIdentityScope(realm.Id, identityScope);
		return Task.CompletedTask;
	}

	public override Task SeedResourceServerAsync(Realm realm, ResourceServer resourceServer)
	{
		resourceSource.AddResourceServer(realm.Id, resourceServer);
		return Task.CompletedTask;
	}

	public override async ValueTask DisposeAsync()
	{
		await scope.DisposeAsync();
		await services.DisposeAsync();
		await database.DisposeAsync();
	}

	private static RealmEntity ToEntity(Realm realm, (int Version, string Json) payload)
		=> new()
		{
			Id = realm.Id,
			Path = realm.Path,
			Domain = realm.Domain,
			DisplayName = realm.DisplayName,
			Enabled = realm.Enabled,
			Internal = realm.Internal,
			OptionsVersion = payload.Version,
			OptionsJson = payload.Json,
		};

	private static ReadOnlySpan<byte> TestProtectorKey
		=>
		[
			0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08,
			0x09, 0x0A, 0x0B, 0x0C, 0x0D, 0x0E, 0x0F, 0x10,
			0x11, 0x12, 0x13, 0x14, 0x15, 0x16, 0x17, 0x18,
			0x19, 0x1A, 0x1B, 0x1C, 0x1D, 0x1E, 0x1F, 0x20,
		];

	private sealed class MutableConfigurationResourceSource : IConfigurationResourceSource
	{
		private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, IdentityScope>> identityScopes = new();
		private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, ResourceServer>> resourceServers = new();

		public IEnumerable<IdentityScope> GetIdentityScopes(string realmId)
			=> identityScopes.TryGetValue(realmId, out var values) ? values.Values : [];

		public IEnumerable<ResourceServer> GetResourceServers(string realmId)
			=> resourceServers.TryGetValue(realmId, out var values) ? values.Values : [];

		public void AddIdentityScope(string realmId, IdentityScope scope)
			=> identityScopes.GetOrAdd(realmId, _ => new(StringComparer.Ordinal))[scope.Name] = scope;

		public void AddResourceServer(string realmId, ResourceServer server)
			=> resourceServers.GetOrAdd(realmId, _ => new(StringComparer.Ordinal))[server.Name] = server;
	}

	private sealed class ConfigurationCompositeStorage : IStorage
	{
		private readonly IConfigurationStoreFactory configuration;
		private readonly ConcurrentDictionary<string, RealmOperationalData> realmData = new(StringComparer.Ordinal);
		private readonly TimeProvider clock;

		public ConfigurationCompositeStorage(
			IConfigurationStoreFactory configuration,
			ServerOptions serverOptions,
			TimeProvider clock)
		{
			this.configuration = configuration;
			this.clock = clock;
			ServerOptions = new ServerOptions(serverOptions);
			Realms = new CoordinatedRealmStore(configuration.Realms, EnsureRealm, RemoveRealm);
			AuthorizeParameters = new AuthorizeParametersStore(new ConcurrentDictionary<string, NameValueCollection>());
		}

		public ServerOptions ServerOptions { get; }

		public IRealmStore Realms { get; }

		public IAuthorizeParametersStore AuthorizeParameters { get; }

		public IAccessTokenStore GetAccessTokenStore(Realm realm)
			=> new AccessTokenStore(GetData(realm).AccessTokens);

		public IRefreshTokenStore GetRefreshTokenStore(Realm realm)
			=> new RefreshTokenStore(GetData(realm).RefreshTokens);

		public IAuthorizationCodeStore GetAuthorizationCodeStore(Realm realm)
			=> new AuthorizationCodeStore(GetData(realm).AuthorizationCodes);

		public IUserConsentStore GetUserConsentStore(Realm realm)
			=> new UserConsentStore(GetData(realm).Consents);

		public IKeyStore GetKeyStore(Realm realm)
			=> configuration.GetKeyStore(realm);

		public IClientStore GetClientStore(Realm realm)
			=> configuration.GetClientStore(realm);

		public IResourceStore GetResourceStore(Realm realm)
			=> configuration.GetResourceStore(realm);

		public IUserSessionStore GetUserSessionStore(Realm realm)
			=> new UserSessionStore(GetData(realm).Sessions, clock);

		public void EnsureRealm(string realmId) => realmData.TryAdd(realmId, new RealmOperationalData());

		private void RemoveRealm(string realmId) => realmData.TryRemove(realmId, out _);

		private RealmOperationalData GetData(Realm realm)
		{
			if (realmData.TryGetValue(realm.Id, out var data))
				return data;

			throw new ArgumentException($"The realm with id '{realm.Id}' is unavailable.", nameof(realm));
		}
	}

	private sealed class CoordinatedRealmStore(
		IRealmStore inner,
		Action<string> realmSaved,
		Action<string> realmDeleted) : IRealmStore
	{
		public ValueTask<Realm?> GetByPathAsync(string path, CancellationToken ct) => inner.GetByPathAsync(path, ct);

		public ValueTask<Realm?> GetByIdAsync(string id, CancellationToken ct) => inner.GetByIdAsync(id, ct);

		public ValueTask<Realm?> GetByDomainAsync(string domain, CancellationToken ct = default)
			=> inner.GetByDomainAsync(domain, ct);

		public IAsyncEnumerable<Realm> GetAllAsync(CancellationToken ct) => inner.GetAllAsync(ct);

		public async ValueTask SaveAsync(Realm realm, CancellationToken ct = default)
		{
			await inner.SaveAsync(realm, ct);
			realmSaved(realm.Id);
		}

		public async ValueTask<bool> DeleteAsync(string realmId, CancellationToken ct = default)
		{
			var deleted = await inner.DeleteAsync(realmId, ct);
			if (deleted)
				realmDeleted(realmId);
			return deleted;
		}
	}

	private sealed class RealmOperationalData
	{
		public ConcurrentDictionary<string, AccessToken> AccessTokens { get; } = new(StringComparer.Ordinal);
		public ConcurrentDictionary<string, RefreshToken> RefreshTokens { get; } = new(StringComparer.Ordinal);
		public ConcurrentDictionary<string, AuthorizationCode> AuthorizationCodes { get; } = new(StringComparer.Ordinal);
		public ConcurrentDictionary<string, Consent> Consents { get; } = new(StringComparer.Ordinal);
		public ConcurrentDictionary<string, UserSession> Sessions { get; } = new(StringComparer.Ordinal);
	}

	private sealed class TestStorageProvider(IStorage storage) : IStorageProvider
	{
		public IStorageSession CreateSession() => new TestStorageSession(storage);
	}

	private sealed class TestStorageSession(IStorage storage) : IStorageSession
	{
		public IStorage GetStorage() => storage;

		public void Dispose()
		{
		}
	}
}
