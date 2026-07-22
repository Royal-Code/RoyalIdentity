using Microsoft.Extensions.DependencyInjection;
using RoyalIdentity.Contracts.Storage;
using RoyalIdentity.Models;
using RoyalIdentity.Models.Scopes;
using RoyalIdentity.Storage.InMemory;
using RoyalIdentity.Storage.InMemory.Extensions;

namespace Tests.Storage.Support;

/// <summary>
/// <see cref="StorageContractHarness"/> backed by <c>AddInMemoryStorage</c> (the transitional fake — ADR-018).
/// This is the only place in the suite allowed to touch <see cref="MemoryStorage"/>/<see cref="RealmMemoryStore"/>:
/// the seed hooks mutate the fake's dictionaries because the read-only configuration contracts offer no writes
/// (DF1). Scenarios never see these types.
/// </summary>
public sealed class InMemoryStorageHarness : StorageContractHarness
{
	private readonly ServiceProvider services;
	private readonly MemoryStorage memoryStorage;
	private Realm realmA = null!;
	private Realm realmB = null!;

	private InMemoryStorageHarness(ServiceProvider services, FakeClock clock)
	{
		this.services = services;
		memoryStorage = services.GetRequiredService<MemoryStorage>();
		Clock = clock;
		Storage = services.GetRequiredService<IStorage>();
		Provider = services.GetRequiredService<IStorageProvider>();
	}

	public override IStorage Storage { get; }

	public override IStorageProvider Provider { get; }

	public override FakeClock Clock { get; }

	public override Realm RealmA => realmA;

	public override Realm RealmB => realmB;

	public override Realm InternalRealm => MemoryStorage.ServerRealm;

	public static async Task<StorageContractHarness> CreateAsync()
	{
		var clock = new FakeClock(Start);
		var collection = new ServiceCollection();
		collection.AddSingleton<TimeProvider>(clock);
		collection.AddInMemoryStorage();

		var services = collection.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
		var harness = new InMemoryStorageHarness(services, clock);

		harness.realmA = await harness.CreateRealmAsync("a");
		harness.realmB = await harness.CreateRealmAsync("b");

		return harness;
	}

	public override Task SeedClientAsync(Client client)
	{
		memoryStorage.GetRealmMemoryStore(client.Realm).Clients[client.Id] = client;
		return Task.CompletedTask;
	}

	public override Task SeedIdentityScopeAsync(Realm realm, IdentityScope identityScope)
	{
		memoryStorage.GetRealmMemoryStore(realm).IdentityScopes[identityScope.Name] = identityScope;
		return Task.CompletedTask;
	}

	public override Task SeedResourceServerAsync(Realm realm, ResourceServer resourceServer)
	{
		memoryStorage.GetRealmMemoryStore(realm).ResourceServers[resourceServer.Name] = resourceServer;
		return Task.CompletedTask;
	}

	public override ValueTask DisposeAsync() => services.DisposeAsync();
}
