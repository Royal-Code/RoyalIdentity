using RoyalIdentity.Contracts.Storage;
using RoyalIdentity.Models;
using RoyalIdentity.Options;
using RoyalIdentity.Storage.EntityFramework.Configuration.Materialization;
using RoyalIdentity.Storage.EntityFramework.Configuration.Resources;
using RoyalIdentity.Storage.EntityFramework.Security.KeyMaterial;

namespace RoyalIdentity.Storage.EntityFramework.Configuration.Stores;

internal sealed class EntityFrameworkConfigurationStoreFactory(
	ConfigurationServerOptionsReader serverOptionsReader,
	EntityFrameworkRealmStore realmStore,
	IConfigurationDbContextAccessor accessor,
	ClientMaterializer clientMaterializer,
	IConfigurationResourceSource resourceSource,
	KeyMaterialProtectorResolver protectorResolver,
	TimeProvider clock) : IConfigurationStoreFactory
{
	public IRealmStore Realms => realmStore;

	public Task<ServerOptions> GetServerOptionsAsync(CancellationToken ct = default)
		=> serverOptionsReader.ReadAsync(ct);

	public IClientStore GetClientStore(Realm realm)
	{
		ArgumentNullException.ThrowIfNull(realm);
		return new EntityFrameworkClientStore(realm.Id, accessor, realmStore, clientMaterializer);
	}

	public IKeyStore GetKeyStore(Realm realm)
	{
		ArgumentNullException.ThrowIfNull(realm);
		return new EntityFrameworkKeyStore(realm.Id, accessor, protectorResolver, clock);
	}

	public IResourceStore GetResourceStore(Realm realm)
	{
		ArgumentNullException.ThrowIfNull(realm);
		return new EntityFrameworkResourceStore(realm.Id, accessor, resourceSource);
	}
}
