using Microsoft.EntityFrameworkCore;
using RoyalIdentity.Configuration;
using RoyalIdentity.Data.Configuration.Entities;
using RoyalIdentity.Storage.EntityFramework.Configuration.Materialization;

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
	ServerOptionsPayloadSerializer serverOptionsSerializer,
	RealmMaterializer realmMaterializer) : IConfigurationSnapshotSource
{
	public async Task<ConfigurationSnapshotData> LoadAsync(CancellationToken ct)
	{
		var db = accessor.DbContext;

		var serverRow = await db.Set<ServerOptionsEntity>()
			.AsNoTracking()
			.SingleOrDefaultAsync(e => e.Id == ServerOptionsEntity.SingletonId, ct)
			?? throw new InvalidOperationException(
				"The Configuration store has no server_options row. Run the migrations and the seed before starting the host.");

		var serverOptions = serverOptionsSerializer.Deserialize(serverRow.PayloadVersion, serverRow.PayloadJson);

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
