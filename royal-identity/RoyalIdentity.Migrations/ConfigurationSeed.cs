using Microsoft.EntityFrameworkCore;
using RoyalIdentity.Data.Configuration;
using RoyalIdentity.Data.Configuration.Entities;
using RoyalIdentity.Models;
using RoyalIdentity.Models.Keys;
using RoyalIdentity.Options;
using RoyalIdentity.Security.Keys;
using RoyalIdentity.Storage.EntityFramework.Configuration.Materialization;
using RoyalIdentity.Storage.EntityFramework.Security.KeyMaterial;
using static RoyalIdentity.Options.Constants;

namespace RoyalIdentity.Migrations;

/// <summary>
/// Idempotent Configuration seed used only by the migration runner and tests. Product and demo data are
/// separate opt-ins; standard scopes remain in the volatile bridge and are never persisted here.
/// </summary>
public sealed class ConfigurationSeed(
	ServerOptionsPayloadSerializer serverSerializer,
	RealmOptionsPayloadSerializer realmSerializer,
	ClientMaterializer clientMaterializer,
	IKeyMaterialProtector protector,
	TimeProvider clock)
{
	public async Task ApplyAsync(
		ConfigurationDbContext db,
		ConfigurationSeedMode mode,
		CancellationToken ct = default)
	{
		ArgumentNullException.ThrowIfNull(db);
		if (mode is ConfigurationSeedMode.None)
			return;

		await using var transaction = await db.Database.BeginTransactionAsync(ct);
		var serverOptions = await EnsureServerOptionsAsync(db, ct);

		if (mode.HasFlag(ConfigurationSeedMode.Product))
			await EnsureProductRealmsAsync(db, serverOptions, ct);
		if (mode.HasFlag(ConfigurationSeedMode.Demo))
			await EnsureDemoRealmAsync(db, serverOptions, ct);

		await db.SaveChangesAsync(ct);

		if (mode.HasFlag(ConfigurationSeedMode.Product))
			await EnsureClientAsync(db, BuildServerAdmin(serverOptions), ct);
		if (mode.HasFlag(ConfigurationSeedMode.Demo))
		{
			foreach (var client in BuildDemoClients(serverOptions))
				await EnsureClientAsync(db, client, ct);
		}

		await EnsureKeysForEnabledRealmsAsync(db, serverOptions, ct);
		await db.SaveChangesAsync(ct);
		await transaction.CommitAsync(ct);
	}

	private async Task<ServerOptions> EnsureServerOptionsAsync(ConfigurationDbContext db, CancellationToken ct)
	{
		var row = await db.ServerOptions
			.SingleOrDefaultAsync(entity => entity.Id == ServerOptionsEntity.SingletonId, ct);
		if (row is not null)
			return serverSerializer.Deserialize(row.PayloadVersion, row.PayloadJson);

		var options = new ServerOptions();
		var payload = serverSerializer.Serialize(options);
		db.ServerOptions.Add(new ServerOptionsEntity
		{
			Id = ServerOptionsEntity.SingletonId,
			PayloadVersion = payload.Version,
			PayloadJson = payload.Json,
			UpdatedAtUtc = clock.GetUtcNow().UtcDateTime,
		});
		await db.SaveChangesAsync(ct);
		return options;
	}

	private async Task EnsureProductRealmsAsync(
		ConfigurationDbContext db,
		ServerOptions serverOptions,
		CancellationToken ct)
	{
		await EnsureRealmAsync(db, new Realm(
			Server.Realms.ServerRealm,
			Server.Realms.ServerDomain,
			Server.Realms.ServerRealm,
			Server.Realms.ServerDisplayName,
			true,
			new RealmOptions(serverOptions)), ct);
		await EnsureRealmAsync(db, new Realm(
			Server.Realms.AccountRealm,
			Server.Realms.AccountDomain,
			Server.Realms.AccountRealm,
			Server.Realms.AccountDisplayName,
			true,
			new RealmOptions(serverOptions)), ct);
		await EnsureRealmAsync(db, new Realm(
			Server.Realms.AdminRealm,
			Server.Realms.AdminDomain,
			Server.Realms.AdminRealm,
			Server.Realms.AdminDisplayName,
			true,
			new RealmOptions(serverOptions)), ct);
	}

	private async Task EnsureDemoRealmAsync(
		ConfigurationDbContext db,
		ServerOptions serverOptions,
		CancellationToken ct)
	{
		var options = new RealmOptions(serverOptions);
		options.Branding.PrimaryColor = "#6366F1";
		await EnsureRealmAsync(db, new Realm(
			"demo_realm",
			"demo.com",
			"demo",
			"Demo Realm",
			false,
			options), ct);
	}

	private async Task EnsureRealmAsync(ConfigurationDbContext db, Realm realm, CancellationToken ct)
	{
		var existing = await db.Realms.SingleOrDefaultAsync(row => row.Id == realm.Id, ct);
		if (existing is not null)
		{
			if (existing.DeletedAtUtc is not null
				|| !string.Equals(existing.Path, realm.Path, StringComparison.Ordinal)
				|| !string.Equals(existing.Domain, realm.Domain, StringComparison.Ordinal)
				|| existing.Internal != realm.Internal)
			{
				throw new InvalidOperationException(
					$"Seed realm '{realm.Id}' conflicts with existing Configuration data.");
			}
			return;
		}

		var payload = realmSerializer.Serialize(realm.Options);
		db.Realms.Add(new RealmEntity
		{
			Id = realm.Id,
			Path = realm.Path,
			Domain = realm.Domain,
			DisplayName = realm.DisplayName,
			Enabled = realm.Enabled,
			Internal = realm.Internal,
			OptionsVersion = payload.Version,
			OptionsJson = payload.Json,
		});
	}

	private async Task EnsureClientAsync(ConfigurationDbContext db, Client client, CancellationToken ct)
	{
		if (await db.Clients.AnyAsync(
			row => row.RealmId == client.Realm.Id && row.ClientId == client.Id,
			ct))
		{
			return;
		}

		var entities = clientMaterializer.ToEntitySet(client);
		db.Clients.Add(entities.Root);
		db.ClientStringValues.AddRange(entities.StringValues);
		db.ClientClaims.AddRange(entities.Claims);
		db.ClientSecrets.AddRange(entities.Secrets);
	}

	private async Task EnsureKeysForEnabledRealmsAsync(
		ConfigurationDbContext db,
		ServerOptions serverOptions,
		CancellationToken ct)
	{
		var realmRows = await db.Realms
			.AsNoTracking()
			.Where(realm => realm.Enabled && realm.DeletedAtUtc == null)
			.ToListAsync(ct);
		var now = clock.GetUtcNow().UtcDateTime;

		foreach (var realmRow in realmRows)
		{
			var options = realmSerializer.Deserialize(
				realmRow.OptionsVersion,
				realmRow.OptionsJson,
				new ServerOptions(serverOptions));
			var currentRows = await db.SigningKeys
				.AsNoTracking()
				.Where(key => key.RealmId == realmRow.Id)
				.Where(key => key.NotBeforeUtc == null || key.NotBeforeUtc <= now)
				.Where(key => key.ExpiresUtc == null || key.ExpiresUtc >= now)
				.ToListAsync(ct);

			var hasMainAlgorithm = false;
			foreach (var row in currentRows)
			{
				await ValidateCurrentKeyAsync(row, ct);
				hasMainAlgorithm |= string.Equals(
					row.SecurityAlgorithm,
					options.Keys.MainSigningCredentialsAlgorithm,
					StringComparison.Ordinal);
			}

			if (hasMainAlgorithm)
				continue;

			var key = KeyParametersFactory.Create(options.Keys);
			key.Created = now;
			if (options.Keys.DefaultSigningCredentialsLifetime is { } lifetime)
			{
				key.NotBefore = now;
				key.Expires = now.Add(lifetime);
			}

			var envelope = await protector.ProtectAsync(key.Key, ct);
			db.SigningKeys.Add(new SigningKeyEntity
			{
				RealmId = realmRow.Id,
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
			});
		}
	}

	private async Task ValidateCurrentKeyAsync(SigningKeyEntity row, CancellationToken ct)
	{
		if (!Enum.IsDefined((KeySerializationFormat)row.SerializationFormat)
			|| !Enum.IsDefined((KeyEncoding)row.Encoding))
		{
			throw new InvalidOperationException("A current signing key has invalid serialization metadata.");
		}

		var envelope = KeyMaterialEnvelope.Parse(row.ProtectorId, row.ProtectedMaterial);
		var material = await protector.UnprotectAsync(envelope, ct);
		var key = new KeyParameters(
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
		_ = key.CreateSigningCredentials();
	}

	private static Client BuildServerAdmin(ServerOptions serverOptions)
	{
		var realm = ProductRealm(
			Server.Realms.ServerRealm,
			Server.Realms.ServerDomain,
			Server.Realms.ServerDisplayName,
			serverOptions);
		return new Client
		{
			Realm = realm,
			Id = "server_admin",
			Name = "Administrative server portal",
			RequireClientSecret = false,
			AllowOfflineAccess = true,
			AllowedIdentityScopes = { "openid", "profile" },
			AllowedResponseTypes = { "code" },
			RedirectUris = { "http://localhost:5200/**", "https://localhost:7200/**" },
		};
	}

	private static IEnumerable<Client> BuildDemoClients(ServerOptions serverOptions)
	{
		var realm = new Realm(
			"demo_realm",
			"demo.com",
			"demo",
			"Demo Realm",
			false,
			new RealmOptions(serverOptions));
		yield return new Client
		{
			Realm = realm,
			Id = "demo_client",
			Name = "Demo Client",
			RequireClientSecret = false,
			AllowOfflineAccess = true,
			AllowedGrantTypes = { "authorization_code", "refresh_token" },
			AllowedIdentityScopes = { "openid", "profile", "email" },
			AllowedResponseTypes = { "code" },
			RedirectUris = { "http://localhost/callback", "http://localhost:5000/**", "https://localhost:5001/**" },
		};
		yield return new Client
		{
			Realm = realm,
			Id = "demo_consent_client",
			Name = "Demo Consent Client",
			RequireClientSecret = false,
			AllowOfflineAccess = true,
			AllowedGrantTypes = { "authorization_code", "refresh_token" },
			AllowedIdentityScopes = { "openid", "profile", "email" },
			AllowedResourceServers = { "apiserver" },
			AllowedResponseTypes = { "code" },
			RequireConsent = true,
			RedirectUris = { "http://localhost/callback", "http://localhost:5000/**", "https://localhost:5001/**" },
		};
	}

	private static Realm ProductRealm(string id, string domain, string displayName, ServerOptions serverOptions)
		=> new(id, domain, id, displayName, true, new RealmOptions(serverOptions));
}
