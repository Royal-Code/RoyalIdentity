using Microsoft.EntityFrameworkCore;
using RoyalIdentity.Models;
using RoyalIdentity.Storage.EntityFramework.Configuration.Materialization;
using Tests.Storage.Configuration.Support;

namespace Tests.Storage.Configuration;

/// <summary>
/// Round-trips a <see cref="Client"/> through the real SQLite schema via <see cref="ClientMaterializer"/>
/// (plan Fase 2, DF5/DF25): every scalar and collection survives; the materialized graph is independent of
/// later mutations; and two realms holding the same client id stay isolated by the realm-bound key.
/// </summary>
public class ConfigurationMaterializationClientTests
{
	private readonly ClientMaterializer materializer = new();

	[Fact]
	public async Task Client_RoundTrips_EveryScalarAndCollection()
	{
		await using var database = await SqliteConfigurationDatabase.CreateMigratedAsync();
		var realm = ConfigurationTestData.BuildRealm("realm-a");
		var original = ConfigurationTestData.BuildFullyPopulatedClient(realm, "client-full");

		await SeedAsync(database, [ConfigurationTestData.BuildRealmRow("realm-a")], [original]);
		var loaded = await LoadAsync(database, realm, "client-full");

		Assert.Equal(original.Id, loaded.Id);
		Assert.Equal(original.Name, loaded.Name);
		Assert.Equal(original.Description, loaded.Description);
		Assert.Equal(original.ClientUri, loaded.ClientUri);
		Assert.Equal(original.LogoUri, loaded.LogoUri);
		Assert.Equal(original.Enabled, loaded.Enabled);
		Assert.Same(realm, loaded.Realm);
		Assert.Equal(original.ProtocolType, loaded.ProtocolType);
		Assert.Equal(original.RequirePkce, loaded.RequirePkce);
		Assert.Equal(original.AllowPlainTextPkce, loaded.AllowPlainTextPkce);
		Assert.Equal(original.ClientType, loaded.ClientType);
		Assert.Equal(original.AllowOfflineAccess, loaded.AllowOfflineAccess);
		Assert.Equal(original.AllowAllResourceServers, loaded.AllowAllResourceServers);
		Assert.Equal(original.IncludeJwtId, loaded.IncludeJwtId);
		Assert.Equal(original.AlwaysSendClientClaims, loaded.AlwaysSendClientClaims);
		Assert.Equal(original.AlwaysIncludeUserClaimsInIdToken, loaded.AlwaysIncludeUserClaimsInIdToken);
		Assert.Equal(original.ClientClaimsPrefix, loaded.ClientClaimsPrefix);
		Assert.Equal(original.EnableLocalLogin, loaded.EnableLocalLogin);
		Assert.Equal(original.UserSsoLifetime, loaded.UserSsoLifetime);
		Assert.Equal(original.AccessTokenLifetime, loaded.AccessTokenLifetime);
		Assert.Equal(original.IdentityTokenLifetime, loaded.IdentityTokenLifetime);
		Assert.Equal(original.AuthorizationCodeLifetime, loaded.AuthorizationCodeLifetime);
		Assert.Equal(original.AbsoluteRefreshTokenLifetime, loaded.AbsoluteRefreshTokenLifetime);
		Assert.Equal(original.SlidingRefreshTokenLifetime, loaded.SlidingRefreshTokenLifetime);
		Assert.Equal(original.ConsentLifetime, loaded.ConsentLifetime);
		Assert.Equal(original.RequireConsent, loaded.RequireConsent);
		Assert.Equal(original.AllowRememberConsent, loaded.AllowRememberConsent);
		Assert.Equal(original.RequireClientSecret, loaded.RequireClientSecret);
		Assert.Equal(original.RefreshTokenExpiration, loaded.RefreshTokenExpiration);
		Assert.Equal(original.RefreshTokenPostConsumedTimeTolerance, loaded.RefreshTokenPostConsumedTimeTolerance);
		Assert.Equal(original.UpdateAccessTokenClaimsOnRefresh, loaded.UpdateAccessTokenClaimsOnRefresh);
		Assert.Equal(original.AllowLogoutWithoutUserConfirmation, loaded.AllowLogoutWithoutUserConfirmation);
		Assert.Equal(original.FrontChannelLogoutSessionRequired, loaded.FrontChannelLogoutSessionRequired);
		Assert.Equal(original.BackChannelLogoutSessionRequired, loaded.BackChannelLogoutSessionRequired);

		Assert.True(loaded.AllowedIdentityScopes.SetEquals(original.AllowedIdentityScopes));
		Assert.True(loaded.AllowedResourceServers.SetEquals(original.AllowedResourceServers));
		Assert.True(loaded.AllowedScopes.SetEquals(original.AllowedScopes));
		Assert.True(loaded.AllowedResponseTypes.SetEquals(original.AllowedResponseTypes));
		Assert.True(loaded.AllowedGrantTypes.SetEquals(original.AllowedGrantTypes));
		Assert.True(loaded.AllowedIdentityTokenSigningAlgorithms.SetEquals(original.AllowedIdentityTokenSigningAlgorithms));
		Assert.True(loaded.AllowedAccessTokenSigningAlgorithms.SetEquals(original.AllowedAccessTokenSigningAlgorithms));
		Assert.True(loaded.IdentityProviderRestrictions.SetEquals(original.IdentityProviderRestrictions));
		Assert.True(loaded.RedirectUris.SetEquals(original.RedirectUris));
		Assert.True(loaded.PostLogoutRedirectUris.SetEquals(original.PostLogoutRedirectUris));
		Assert.True(loaded.AllowedCorsOrigins.SetEquals(original.AllowedCorsOrigins));
		Assert.True(loaded.FrontChannelLogoutUri.SetEquals(original.FrontChannelLogoutUri));
		Assert.True(loaded.BackChannelLogoutUri.SetEquals(original.BackChannelLogoutUri));

		// The case-insensitive CORS comparer is restored by the adapter, not inherited from any collation.
		Assert.Contains("https://CLIENT.EXAMPLE", loaded.AllowedCorsOrigins);

		var loadedClaims = loaded.Claims
			.Select(c => (c.Type, c.Value, c.ValueType, c.Issuer, c.OriginalIssuer))
			.ToHashSet();
		var originalClaims = original.Claims
			.Select(c => (c.Type, c.Value, c.ValueType, c.Issuer, c.OriginalIssuer))
			.ToHashSet();
		Assert.True(loadedClaims.SetEquals(originalClaims));

		var loadedSecrets = loaded.ClientSecrets
			.Select(s => (s.Type, s.Value, s.Description, s.Expiration))
			.ToHashSet();
		var originalSecrets = original.ClientSecrets
			.Select(s => (s.Type, s.Value, s.Description, s.Expiration))
			.ToHashSet();
		Assert.True(loadedSecrets.SetEquals(originalSecrets));
	}

	[Fact]
	public async Task MaterializedClient_IsIndependentOfLaterMutations()
	{
		await using var database = await SqliteConfigurationDatabase.CreateMigratedAsync();
		var realm = ConfigurationTestData.BuildRealm("realm-a");
		var original = ConfigurationTestData.BuildFullyPopulatedClient(realm, "client-full");
		await SeedAsync(database, [ConfigurationTestData.BuildRealmRow("realm-a")], [original]);

		var first = await LoadAsync(database, realm, "client-full");
		first.Name = "mutated-in-memory";
		first.RedirectUris.Add("https://client.example/injected");
		first.AllowedScopes.Clear();

		var second = await LoadAsync(database, realm, "client-full");

		// Mutating a materialized graph never persists without an explicit write (plan DF25).
		Assert.Equal("Full Client", second.Name);
		Assert.DoesNotContain("https://client.example/injected", second.RedirectUris);
		Assert.True(second.AllowedScopes.SetEquals(original.AllowedScopes));
	}

	[Fact]
	public async Task SameClientId_InTwoRealms_StaysIsolated()
	{
		await using var database = await SqliteConfigurationDatabase.CreateMigratedAsync();
		var realmA = ConfigurationTestData.BuildRealm("realm-a");
		var realmB = ConfigurationTestData.BuildRealm("realm-b");

		var clientA = ConfigurationTestData.BuildFullyPopulatedClient(realmA, "shared-id");
		var clientB = ConfigurationTestData.BuildFullyPopulatedClient(realmB, "shared-id");
		clientB.RedirectUris.Add("https://client.example/only-in-b");

		await SeedAsync(
			database,
			[ConfigurationTestData.BuildRealmRow("realm-a"), ConfigurationTestData.BuildRealmRow("realm-b")],
			[clientA, clientB]);

		var loadedA = await LoadAsync(database, realmA, "shared-id");
		var loadedB = await LoadAsync(database, realmB, "shared-id");

		Assert.DoesNotContain("https://client.example/only-in-b", loadedA.RedirectUris);
		Assert.Contains("https://client.example/only-in-b", loadedB.RedirectUris);
		Assert.Same(realmA, loadedA.Realm);
		Assert.Same(realmB, loadedB.Realm);
	}

	[Fact]
	public void Materializer_RejectsRootFromAnotherRealm()
	{
		var realm = ConfigurationTestData.BuildRealm("realm-a");
		var set = materializer.ToEntitySet(ConfigurationTestData.BuildFullyPopulatedClient(realm, "client-a"));
		set.Root.RealmId = "realm-b";

		Assert.Throws<ConfigurationMaterializationException>(
			() => materializer.ToClient(set.Root, set.StringValues, set.Claims, set.Secrets, realm));
	}

	[Theory]
	[InlineData("string-value")]
	[InlineData("claim")]
	[InlineData("secret")]
	public void Materializer_RejectsSatelliteRowsFromAnotherClient(string satellite)
	{
		var realm = ConfigurationTestData.BuildRealm("realm-a");
		var set = materializer.ToEntitySet(ConfigurationTestData.BuildFullyPopulatedClient(realm, "client-a"));

		switch (satellite)
		{
			case "string-value":
				set.StringValues[0].ClientId = "client-b";
				break;
			case "claim":
				set.Claims[0].ClientId = "client-b";
				break;
			case "secret":
				set.Secrets[0].ClientId = "client-b";
				break;
		}

		Assert.Throws<ConfigurationMaterializationException>(
			() => materializer.ToClient(set.Root, set.StringValues, set.Claims, set.Secrets, realm));
	}

	[Fact]
	public void Materializer_RejectsUnknownStringValueKind()
	{
		var realm = ConfigurationTestData.BuildRealm("realm-a");
		var set = materializer.ToEntitySet(ConfigurationTestData.BuildFullyPopulatedClient(realm, "client-a"));
		set.StringValues[0].Kind = "unknown_kind";

		Assert.Throws<ConfigurationMaterializationException>(
			() => materializer.ToClient(set.Root, set.StringValues, set.Claims, set.Secrets, realm));
	}

	[Theory]
	[InlineData("client-type")]
	[InlineData("refresh-token-expiration")]
	public void Materializer_RejectsInvalidEnumValues(string property)
	{
		var realm = ConfigurationTestData.BuildRealm("realm-a");
		var set = materializer.ToEntitySet(ConfigurationTestData.BuildFullyPopulatedClient(realm, "client-a"));

		if (property == "client-type")
			set.Root.ClientType = int.MaxValue;
		else
			set.Root.RefreshTokenExpiration = int.MaxValue;

		Assert.Throws<ConfigurationMaterializationException>(
			() => materializer.ToClient(set.Root, set.StringValues, set.Claims, set.Secrets, realm));
	}

	private async Task SeedAsync(SqliteConfigurationDatabase database, IEnumerable<RoyalIdentity.Data.Configuration.Entities.RealmEntity> realms, IEnumerable<Client> clients)
	{
		await using var context = database.NewContext();
		context.Realms.AddRange(realms);

		foreach (var client in clients)
		{
			var set = materializer.ToEntitySet(client);
			context.Clients.Add(set.Root);
			context.ClientStringValues.AddRange(set.StringValues);
			context.ClientClaims.AddRange(set.Claims);
			context.ClientSecrets.AddRange(set.Secrets);
		}

		await context.SaveChangesAsync();
	}

	private async Task<Client> LoadAsync(SqliteConfigurationDatabase database, Realm realm, string clientId)
	{
		await using var context = database.NewContext();

		var root = await context.Clients.AsNoTracking()
			.SingleAsync(c => c.RealmId == realm.Id && c.ClientId == clientId);
		var stringValues = await context.ClientStringValues.AsNoTracking()
			.Where(v => v.RealmId == realm.Id && v.ClientId == clientId).ToListAsync();
		var claims = await context.ClientClaims.AsNoTracking()
			.Where(c => c.RealmId == realm.Id && c.ClientId == clientId).ToListAsync();
		var secrets = await context.ClientSecrets.AsNoTracking()
			.Where(s => s.RealmId == realm.Id && s.ClientId == clientId).ToListAsync();

		return materializer.ToClient(root, stringValues, claims, secrets, realm);
	}
}
