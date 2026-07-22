using Microsoft.EntityFrameworkCore;
using RoyalIdentity.Contracts.Storage;
using RoyalIdentity.Data.Configuration.Entities;
using RoyalIdentity.Security.Keys;
using RoyalIdentity.Storage.EntityFramework.Configuration.Materialization;
using RoyalIdentity.Storage.EntityFramework.Security.KeyMaterial;

namespace RoyalIdentity.Storage.EntityFramework.Configuration.Stores;

internal sealed class EntityFrameworkKeyStore(
	string realmId,
	IConfigurationDbContextAccessor accessor,
	KeyMaterialProtectorResolver protectorResolver,
	TimeProvider clock) : IKeyStore
{
	public async Task AddKeyAsync(KeyParameters key, CancellationToken ct)
	{
		ArgumentNullException.ThrowIfNull(key);
		var db = accessor.DbContext;
		if (!await IsRealmLiveAsync(ct))
			throw new ArgumentException("The realm is unavailable.", nameof(realmId));

		var protector = protectorResolver.GetForWrite();
		var envelope = await protector.ProtectAsync(key.Key, ct);
		var row = new SigningKeyEntity
		{
			RealmId = realmId,
			KeyId = key.KeyId,
			Name = key.Name,
			SecurityAlgorithm = key.SecurityAlgorithm,
			SerializationFormat = (int)key.Format,
			Encoding = (int)key.Encoding,
			CreatedUtc = key.Created,
			NotBeforeUtc = key.NotBefore,
			ExpiresUtc = key.Expires,
			ProtectorId = envelope.ProtectorId,
			ProtectedMaterial = envelope.ToPersistedPayload(),
		};

		db.Add(row);
		try
		{
			await db.SaveChangesAsync(ct);
		}
		finally
		{
			db.Entry(row).State = EntityState.Detached;
		}
	}

	public Task<IReadOnlyList<string>> ListAllCurrentKeysIdsAsync(
		DateTime? now = null,
		CancellationToken ct = default)
	{
		var instant = now ?? clock.GetUtcNow().UtcDateTime;
		return ListIdsAsync(
			KeysFromLiveRealm()
				.Where(key => key.NotBeforeUtc == null || key.NotBeforeUtc <= instant)
				.Where(key => key.ExpiresUtc == null || key.ExpiresUtc >= instant),
			ct);
	}

	public Task<IReadOnlyList<string>> ListAllKeysIdsAsync(
		DateTime? now = null,
		CancellationToken ct = default)
	{
		var instant = now ?? clock.GetUtcNow().UtcDateTime;
		return ListIdsAsync(
			KeysFromLiveRealm()
				.Where(key => key.NotBeforeUtc == null || key.NotBeforeUtc <= instant),
			ct);
	}

	public async Task<KeyParameters> GetKeyAsync(string keyId, CancellationToken ct)
	{
		var row = await KeysFromLiveRealm()
			.SingleOrDefaultAsync(key => key.KeyId == keyId, ct);

		if (row is null)
			throw MissingKey(keyId);

		return await MaterializeAsync(row, ct);
	}

	public async Task<IReadOnlyList<KeyParameters>> GetKeysAsync(
		IReadOnlyList<string> keyIds,
		CancellationToken ct)
	{
		ArgumentNullException.ThrowIfNull(keyIds);
		if (keyIds.Count is 0)
			return [];

		var rows = await KeysFromLiveRealm()
			.Where(key => keyIds.Contains(key.KeyId))
			.ToListAsync(ct);
		var rowsById = rows.ToDictionary(key => key.KeyId, StringComparer.Ordinal);
		var keys = new List<KeyParameters>(keyIds.Count);

		foreach (var keyId in keyIds)
		{
			if (!rowsById.TryGetValue(keyId, out var row))
				throw MissingKey(keyId);

			keys.Add(await MaterializeAsync(row, ct));
		}

		return keys;
	}

	private IQueryable<SigningKeyEntity> KeysFromLiveRealm()
	{
		var db = accessor.DbContext;
		return db.Set<SigningKeyEntity>()
			.AsNoTracking()
			.Where(key => key.RealmId == realmId)
			.Where(key => db.Set<RealmEntity>()
				.Any(realm => realm.Id == key.RealmId && realm.DeletedAtUtc == null));
	}

	private Task<bool> IsRealmLiveAsync(CancellationToken ct)
		=> accessor.DbContext.Set<RealmEntity>()
			.AsNoTracking()
			.AnyAsync(realm => realm.Id == realmId && realm.DeletedAtUtc == null, ct);

	private static async Task<IReadOnlyList<string>> ListIdsAsync(
		IQueryable<SigningKeyEntity> query,
		CancellationToken ct)
		=> await query
			.OrderBy(key => key.CreatedUtc)
			.ThenBy(key => key.KeyId)
			.Select(key => key.KeyId)
			.ToListAsync(ct);

	private async Task<KeyParameters> MaterializeAsync(SigningKeyEntity row, CancellationToken ct)
	{
		if (!Enum.IsDefined(typeof(KeySerializationFormat), row.SerializationFormat))
			throw ConfigurationMaterializationException.InvalidSigningKeyEnum(nameof(KeyParameters.Format));
		if (!Enum.IsDefined(typeof(KeyEncoding), row.Encoding))
			throw ConfigurationMaterializationException.InvalidSigningKeyEnum(nameof(KeyParameters.Encoding));

		var envelope = KeyMaterialEnvelope.Parse(row.ProtectorId, row.ProtectedMaterial);
		var protector = protectorResolver.GetForRead(envelope.ProtectorId);
		var material = await protector.UnprotectAsync(envelope, ct);
		return new KeyParameters(
			row.KeyId,
			row.Name,
			row.SecurityAlgorithm,
			(KeySerializationFormat)row.SerializationFormat,
			(KeyEncoding)row.Encoding,
			material,
			row.CreatedUtc)
		{
			NotBefore = row.NotBeforeUtc,
			Expires = row.ExpiresUtc,
		};
	}

	private static ArgumentException MissingKey(string keyId)
		=> new($"The key with the Id '{keyId}' was not found", nameof(keyId));
}
