using RoyalIdentity.Contracts.Storage;
using RoyalIdentity.Models;
using RoyalIdentity.Options;
using RoyalIdentity.Storage.EntityFramework.Configuration.Materialization;
using RoyalIdentity.Storage.EntityFramework.Configuration.Resources;

namespace RoyalIdentity.Storage.EntityFramework.Configuration.Stores;

internal sealed class EntityFrameworkConfigurationStoreFactory(
	ConfigurationServerOptionsReader serverOptionsReader,
	EntityFrameworkRealmStore realmStore,
	IConfigurationDbContextAccessor accessor,
	ClientMaterializer clientMaterializer,
	IConfigurationResourceSource resourceSource) : IConfigurationStoreFactory
{
	public IRealmStore Realms => realmStore;

	public Task<ServerOptions> GetServerOptionsAsync(CancellationToken ct = default)
		=> serverOptionsReader.ReadAsync(ct);

	public IClientStore GetClientStore(Realm realm)
	{
		ArgumentNullException.ThrowIfNull(realm);
		return new EntityFrameworkClientStore(realm.Id, accessor, realmStore, clientMaterializer);
	}

	public IResourceStore GetResourceStore(Realm realm)
	{
		ArgumentNullException.ThrowIfNull(realm);
		return new EntityFrameworkResourceStore(realm.Id, accessor, resourceSource);
	}
}
