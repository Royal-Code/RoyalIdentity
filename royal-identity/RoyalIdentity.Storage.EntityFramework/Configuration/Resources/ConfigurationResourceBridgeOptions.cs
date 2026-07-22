using RoyalIdentity.Models.Scopes;
using System.IdentityModel.Tokens.Jwt;
using static RoyalIdentity.Options.Constants;

namespace RoyalIdentity.Storage.EntityFramework.Configuration.Resources;

/// <summary>
/// Product configuration for the transitional volatile resource bridge. Standard OIDC identity scopes are
/// enabled for every live realm. Additional identity scopes and resource servers are explicitly assigned to
/// a realm; demo data is never included implicitly.
/// </summary>
public sealed class ConfigurationResourceBridgeOptions
{
	private readonly Dictionary<string, List<IdentityScope>> identityScopesByRealm = new(StringComparer.Ordinal);
	private readonly Dictionary<string, List<ResourceServer>> resourceServersByRealm = new(StringComparer.Ordinal);

	public ConfigurationResourceBridgeOptions()
	{
		foreach (var scope in CreateStandardIdentityScopes())
			StandardIdentityScopes.Add(scope);
	}

	/// <summary>Templates copied into every live realm binding.</summary>
	public IList<IdentityScope> StandardIdentityScopes { get; } = new List<IdentityScope>();

	/// <summary>Adds one realm-specific identity-scope template.</summary>
	public void AddIdentityScope(string realmId, IdentityScope identityScope)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(realmId);
		ArgumentNullException.ThrowIfNull(identityScope);
		GetOrAdd(identityScopesByRealm, realmId).Add(identityScope);
	}

	/// <summary>Adds one realm-specific resource-server template.</summary>
	public void AddResourceServer(string realmId, ResourceServer resourceServer)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(realmId);
		ArgumentNullException.ThrowIfNull(resourceServer);
		GetOrAdd(resourceServersByRealm, realmId).Add(resourceServer);
	}

	internal IEnumerable<IdentityScope> GetIdentityScopes(string realmId)
		=> identityScopesByRealm.TryGetValue(realmId, out var scopes) ? scopes : [];

	internal IEnumerable<ResourceServer> GetResourceServers(string realmId)
		=> resourceServersByRealm.TryGetValue(realmId, out var servers) ? servers : [];

	internal void AddDemoResources(string realmId)
	{
		var server = new ResourceServer(
			ScopeVisibility.Public,
			"apiserver",
			"API Server",
			"Access to the API Server")
		{
			Scopes =
			[
				new Scope(ScopeVisibility.Public, "api", "API", "Access to the API")
				{
					Required = true,
					Emphasize = false,
				},
				new Scope(ScopeVisibility.Public, "api:read", "API read", "Read values from the API")
				{
					ShowInDiscoveryDocument = true,
				},
				new Scope(ScopeVisibility.Public, "api:write", "API write", "Write values from the API")
				{
					Emphasize = true,
					ShowInDiscoveryDocument = true,
				},
			],
			ProtectedResources =
			[
				new ProtectedResource("https://api.demo.local/apiserver") { DisplayName = "API Server" },
			],
		};

		AddResourceServer(realmId, server);
	}

	private static List<TValue> GetOrAdd<TValue>(Dictionary<string, List<TValue>> values, string realmId)
	{
		if (!values.TryGetValue(realmId, out var realmValues))
		{
			realmValues = [];
			values.Add(realmId, realmValues);
		}

		return realmValues;
	}

	private static IEnumerable<IdentityScope> CreateStandardIdentityScopes()
	{
		yield return new IdentityScope(
			ScopeVisibility.Public,
			Server.StandardScopes.OpenId,
			"Your user identifier",
			"Your user identifier",
			[JwtRegisteredClaimNames.Sub])
		{
			Required = true,
			ShowInDiscoveryDocument = true,
		};

		yield return new IdentityScope(
			ScopeVisibility.Public,
			Server.StandardScopes.Profile,
			"Your profile data",
			"Your profile data",
			[
				JwtRegisteredClaimNames.Name,
				JwtRegisteredClaimNames.FamilyName,
				JwtRegisteredClaimNames.GivenName,
				Jwt.ClaimTypes.MiddleName,
				Jwt.ClaimTypes.NickName,
				Jwt.ClaimTypes.PreferredUserName,
				Jwt.ClaimTypes.Profile,
				Jwt.ClaimTypes.Picture,
				JwtRegisteredClaimNames.Website,
				Jwt.ClaimTypes.Gender,
				JwtRegisteredClaimNames.Birthdate,
				Jwt.ClaimTypes.ZoneInfo,
				Jwt.ClaimTypes.Locale,
				Jwt.ClaimTypes.UpdatedAt,
			]) { ShowInDiscoveryDocument = true };

		yield return new IdentityScope(
			ScopeVisibility.Public,
			Server.StandardScopes.Email,
			"Your email address",
			"Your email address",
			[JwtRegisteredClaimNames.Email, Jwt.ClaimTypes.EmailVerified])
		{
			ShowInDiscoveryDocument = true,
		};

		yield return new IdentityScope(
			ScopeVisibility.Public,
			Server.StandardScopes.Address,
			"Your address",
			"Your address",
			[Jwt.ClaimTypes.Address])
		{
			ShowInDiscoveryDocument = true,
		};

		yield return new IdentityScope(
			ScopeVisibility.Public,
			Server.StandardScopes.Phone,
			"Your phone number",
			"Your phone number",
			[Jwt.ClaimTypes.PhoneNumber, Jwt.ClaimTypes.PhoneNumberVerified])
		{
			ShowInDiscoveryDocument = true,
		};
	}
}
