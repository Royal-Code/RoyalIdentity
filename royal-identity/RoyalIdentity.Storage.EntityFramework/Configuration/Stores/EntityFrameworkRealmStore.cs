using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using RoyalIdentity.Contracts.Storage;
using RoyalIdentity.Data.Configuration.Entities;
using RoyalIdentity.Models;
using RoyalIdentity.Options;
using RoyalIdentity.Storage.EntityFramework.Configuration.Materialization;

namespace RoyalIdentity.Storage.EntityFramework.Configuration.Stores;

internal sealed class EntityFrameworkRealmStore(
	IConfigurationDbContextAccessor accessor,
	ConfigurationServerOptionsReader serverOptionsReader,
	RealmOptionsPayloadSerializer realmOptionsSerializer,
	RealmMaterializer realmMaterializer,
	TimeProvider clock) : IRealmStore
{
	public async ValueTask<Realm?> GetByPathAsync(string path, CancellationToken ct)
	{
		var row = await LiveRealms()
			.SingleOrDefaultAsync(realm => realm.Path == path, ct);

		return await MaterializeAsync(row, ct);
	}

	public async ValueTask<Realm?> GetByIdAsync(string id, CancellationToken ct)
	{
		var row = await LiveRealms()
			.SingleOrDefaultAsync(realm => realm.Id == id, ct);

		return await MaterializeAsync(row, ct);
	}

	public async ValueTask<Realm?> GetByDomainAsync(string domain, CancellationToken ct = default)
	{
		var row = await LiveRealms()
			.SingleOrDefaultAsync(realm => realm.Domain == domain, ct);

		return await MaterializeAsync(row, ct);
	}

	public async IAsyncEnumerable<Realm> GetAllAsync(
		[EnumeratorCancellation] CancellationToken ct)
	{
		var serverOptions = await serverOptionsReader.ReadAsync(ct);

		await foreach (var row in LiveRealms().AsAsyncEnumerable().WithCancellation(ct))
			yield return realmMaterializer.ToRealm(row, new ServerOptions(serverOptions));
	}

	public async ValueTask SaveAsync(Realm realm, CancellationToken ct = default)
	{
		ArgumentNullException.ThrowIfNull(realm);
		EnsureCanonicalDomain(realm.Domain);

		var db = accessor.DbContext;
		var row = await db.Set<RealmEntity>()
			.SingleOrDefaultAsync(entity => entity.Id == realm.Id, ct);
		var payload = realmOptionsSerializer.Serialize(realm.Options);

		if (row is null)
		{
			row = new RealmEntity
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
			db.Add(row);
		}
		else
		{
			if (row.DeletedAtUtc is not null)
				throw new InvalidOperationException($"Deleted realm '{realm.Id}' cannot be restored through SaveAsync.");

			if (row.Internal
				&& (!string.Equals(row.Path, realm.Path, StringComparison.Ordinal)
					|| !string.Equals(row.Domain, realm.Domain, StringComparison.Ordinal)
					|| !realm.Internal))
			{
				throw new InvalidOperationException($"Internal realm '{realm.Id}' has immutable identity fields.");
			}

			row.Path = realm.Path;
			row.Domain = realm.Domain;
			row.DisplayName = realm.DisplayName;
			row.Enabled = realm.Enabled;
			row.Internal = realm.Internal;
			row.OptionsVersion = payload.Version;
			row.OptionsJson = payload.Json;
		}

		await db.SaveChangesAsync(ct);
	}

	public async ValueTask<bool> DeleteAsync(string realmId, CancellationToken ct = default)
	{
		var db = accessor.DbContext;
		var row = await db.Set<RealmEntity>()
			.SingleOrDefaultAsync(entity => entity.Id == realmId && entity.DeletedAtUtc == null, ct);

		if (row is null || row.Internal)
			return false;

		row.DeletedAtUtc = clock.GetUtcNow().UtcDateTime;
		await db.SaveChangesAsync(ct);
		return true;
	}

	private IQueryable<RealmEntity> LiveRealms()
		=> accessor.DbContext.Set<RealmEntity>()
			.AsNoTracking()
			.Where(realm => realm.DeletedAtUtc == null);

	private async ValueTask<Realm?> MaterializeAsync(RealmEntity? row, CancellationToken ct)
	{
		if (row is null)
			return null;

		var serverOptions = await serverOptionsReader.ReadAsync(ct);
		return realmMaterializer.ToRealm(row, serverOptions);
	}

	private static void EnsureCanonicalDomain(string domain)
	{
		if (!string.Equals(domain, domain.ToLowerInvariant(), StringComparison.Ordinal))
		{
			throw new ArgumentException(
				"Realm domain must already be normalized to lowercase before it reaches the EF store.",
				nameof(domain));
		}
	}
}
