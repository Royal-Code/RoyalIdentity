using Microsoft.EntityFrameworkCore;
using RoyalIdentity.Contracts.Storage;
using RoyalIdentity.Data.Configuration.Entities;
using RoyalIdentity.Models;
using RoyalIdentity.Storage.EntityFramework.Configuration.Materialization;

namespace RoyalIdentity.Storage.EntityFramework.Configuration.Stores;

internal sealed class EntityFrameworkClientStore(
	string realmId,
	IConfigurationDbContextAccessor accessor,
	IRealmStore realmStore,
	ClientMaterializer materializer) : IClientStore
{
	public Task<Client?> FindClientByIdAsync(string clientId, CancellationToken ct)
		=> FindAsync(clientId, onlyEnabled: false, ct);

	public Task<Client?> FindEnabledClientByIdAsync(string clientId, CancellationToken ct)
		=> FindAsync(clientId, onlyEnabled: true, ct);

	private async Task<Client?> FindAsync(string clientId, bool onlyEnabled, CancellationToken ct)
	{
		var db = accessor.DbContext;
		var query = db.Set<ClientEntity>()
			.AsNoTracking()
			.Where(client => client.RealmId == realmId && client.ClientId == clientId)
			.Where(client => db.Set<RealmEntity>()
				.Any(realm => realm.Id == client.RealmId && realm.DeletedAtUtc == null));

		if (onlyEnabled)
			query = query.Where(client => client.Enabled);

		var root = await query.SingleOrDefaultAsync(ct);
		if (root is null)
			return null;

		var realm = await realmStore.GetByIdAsync(realmId, ct);
		if (realm is null)
			return null;

		var stringValues = await db.Set<ClientStringValueEntity>()
			.AsNoTracking()
			.Where(value => value.RealmId == realmId && value.ClientId == clientId)
			.ToListAsync(ct);
		var claims = await db.Set<ClientClaimEntity>()
			.AsNoTracking()
			.Where(claim => claim.RealmId == realmId && claim.ClientId == clientId)
			.ToListAsync(ct);
		var secrets = await db.Set<ClientSecretEntity>()
			.AsNoTracking()
			.Where(secret => secret.RealmId == realmId && secret.ClientId == clientId)
			.ToListAsync(ct);

		return materializer.ToClient(root, stringValues, claims, secrets, realm);
	}
}
