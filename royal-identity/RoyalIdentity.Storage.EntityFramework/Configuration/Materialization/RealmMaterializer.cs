using RoyalIdentity.Data.Configuration.Entities;
using RoyalIdentity.Models;
using RoyalIdentity.Options;

namespace RoyalIdentity.Storage.EntityFramework.Configuration.Materialization;

/// <summary>
/// Maps a persisted <see cref="RealmEntity"/> to the core <see cref="Realm"/> (plan DF5/DF25). The relational
/// identity/binding columns become the realm's fields; the versioned JSON payload becomes its
/// <see cref="RealmOptions"/>, re-bound to the authoritative <see cref="ServerOptions"/> supplied by the caller
/// (from the snapshot), never the one that happened to be current when the payload was written.
/// </summary>
public sealed class RealmMaterializer(RealmOptionsPayloadSerializer realmOptionsSerializer)
{
	public Realm ToRealm(RealmEntity entity, ServerOptions serverOptions)
	{
		ArgumentNullException.ThrowIfNull(entity);
		ArgumentNullException.ThrowIfNull(serverOptions);

		var options = realmOptionsSerializer.Deserialize(entity.OptionsVersion, entity.OptionsJson, serverOptions);

		return new Realm(entity.Id, entity.Domain, entity.Path, entity.DisplayName, entity.Internal, options)
		{
			Enabled = entity.Enabled,
		};
	}
}
