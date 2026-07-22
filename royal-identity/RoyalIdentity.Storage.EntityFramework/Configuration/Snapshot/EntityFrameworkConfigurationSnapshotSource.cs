using Microsoft.EntityFrameworkCore;
using RoyalIdentity.Configuration;
using RoyalIdentity.Data.Configuration.Entities;
using RoyalIdentity.Storage.EntityFramework.Configuration.Materialization;
using RoyalIdentity.Storage.EntityFramework.Configuration.Stores;

namespace RoyalIdentity.Storage.EntityFramework.Configuration.Snapshot;

/// <summary>
/// EF <see cref="IConfigurationSnapshotSource"/>: reads the authoritative server options and the live (non-
/// tombstoned) realms from the Configuration database and materializes an independent graph (plan DF7). It
/// reads through the scoped <see cref="IConfigurationDbContextAccessor"/> — never <c>IStorage.ServerOptions</c>
/// — with async, cancellable queries only. A missing server-options row is fail-closed: the caller (the hosted
/// refresher on bootstrap) aborts rather than publishing an empty snapshot.
/// </summary>
internal sealed class EntityFrameworkConfigurationSnapshotSource(
	IConfigurationDbContextAccessor accessor,
	ConfigurationServerOptionsReader serverOptionsReader,
	RealmMaterializer realmMaterializer) : IConfigurationSnapshotSource
{
	public async Task<ConfigurationSnapshotData> LoadAsync(CancellationToken ct)
	{
		var db = accessor.DbContext;
		var serverOptions = await serverOptionsReader.ReadAsync(ct);

		var realmRows = await db.Set<RealmEntity>()
			.AsNoTracking()
			.Where(r => r.DeletedAtUtc == null)
			.ToListAsync(ct);

		var realms = realmRows
			.Select(r => realmMaterializer.ToRealm(r, serverOptions))
			.ToList();

		return new ConfigurationSnapshotData
		{
			ServerOptions = serverOptions,
			Realms = realms,
		};
	}
}
