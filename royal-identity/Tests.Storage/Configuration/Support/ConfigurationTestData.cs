using RoyalIdentity.Data.Configuration.Entities;
using RoyalIdentity.Models;
using RoyalIdentity.Options;
using System.Security.Claims;

namespace Tests.Storage.Configuration.Support;

/// <summary>
/// Test data builders for the Configuration materialization/round-trip tests. Kept in one place so the
/// "fully populated client" (every scalar off its default, every collection non-empty) stays exhaustive.
/// </summary>
internal static class ConfigurationTestData
{
	public static readonly ServerOptions ServerOptions = new();

	public static Realm BuildRealm(string id)
		=> new(id, $"{id}.contract.test", id, $"Realm {id}", false, new RealmOptions(ServerOptions));

	/// <summary>A minimal, valid realm row so a client's FK resolves; options payload fidelity is tested elsewhere.</summary>
	public static RealmEntity BuildRealmRow(string id)
		=> new()
		{
			Id = id,
			Path = id,
			Domain = $"{id}.contract.test",
			DisplayName = $"Realm {id}",
			Enabled = true,
			Internal = false,
			OptionsVersion = 1,
			OptionsJson = "{}",
		};

	/// <summary>
	/// A client with every scalar set away from its default and every collection non-empty, so a round-trip
	/// that loses any field is caught. CORS origin is intentionally mixed-case to prove the comparer survives.
	/// </summary>
	public static Client BuildFullyPopulatedClient(Realm realm, string clientId)
	{
		var client = new Client
		{
			Id = clientId,
			Name = "Full Client",
			Description = "a fully populated client",
			ClientUri = "https://client.example/info",
			LogoUri = "https://client.example/logo.png",
			Enabled = false,
			Realm = realm,
			ProtocolType = "custom-protocol",
			RequirePkce = false,
			AllowPlainTextPkce = true,
			ClientType = ClientType.Confidential,
			AllowOfflineAccess = true,
			AllowAllResourceServers = true,
			IncludeJwtId = false,
			AlwaysSendClientClaims = false,
			AlwaysIncludeUserClaimsInIdToken = true,
			ClientClaimsPrefix = "cpref_",
			EnableLocalLogin = false,
			UserSsoLifetime = 3600,
			AccessTokenLifetime = 1200,
			IdentityTokenLifetime = 700,
			AuthorizationCodeLifetime = 90,
			AbsoluteRefreshTokenLifetime = 999_999,
			SlidingRefreshTokenLifetime = 55_555,
			ConsentLifetime = 4321,
			RequireConsent = true,
			AllowRememberConsent = false,
			RequireClientSecret = false,
			RefreshTokenExpiration = TokenExpiration.Sliding,
			RefreshTokenPostConsumedTimeTolerance = TimeSpan.FromSeconds(42),
			UpdateAccessTokenClaimsOnRefresh = true,
			AllowLogoutWithoutUserConfirmation = true,
			FrontChannelLogoutSessionRequired = false,
			BackChannelLogoutSessionRequired = false,
		};

		client.AllowedIdentityScopes.UnionWith(["openid", "profile"]);
		client.AllowedResourceServers.UnionWith(["rs1", "rs2"]);
		client.AllowedScopes.UnionWith(["api.read", "api.write"]);
		client.AllowedResponseTypes.UnionWith(["id_token"]);
		client.AllowedGrantTypes.UnionWith(["client_credentials"]);
		client.AllowedIdentityTokenSigningAlgorithms.UnionWith(["RS256"]);
		client.AllowedAccessTokenSigningAlgorithms.UnionWith(["ES256"]);
		client.IdentityProviderRestrictions.UnionWith(["google"]);
		client.RedirectUris.UnionWith(["https://client.example/cb"]);
		client.PostLogoutRedirectUris.UnionWith(["https://client.example/logout-cb"]);
		client.AllowedCorsOrigins.UnionWith(["https://Client.Example"]);
		client.FrontChannelLogoutUri.UnionWith(["https://client.example/fc"]);
		client.BackChannelLogoutUri.UnionWith(["https://client.example/bc"]);

		client.Claims.Add(new Claim("role", "admin"));
		client.Claims.Add(new Claim("dept", "engineering"));

		client.ClientSecrets.Add(new ClientSecret("hash-1", "primary", new DateTime(2027, 1, 1, 0, 0, 0, DateTimeKind.Utc)));
		client.ClientSecrets.Add(new ClientSecret("hash-2"));

		return client;
	}
}
