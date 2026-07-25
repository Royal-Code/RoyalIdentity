using System.Collections.Specialized;
using System.Security.Claims;
using RoyalIdentity.Models;
using RoyalIdentity.Models.Scopes;
using RoyalIdentity.Models.Tokens;

namespace Tests.Storage.Operational.Support;

/// <summary>
/// Deliberately "full" operational models for the payload round-trip scenarios: every collection populated,
/// every optional field set, so a serializer that silently drops something fails a test instead of passing
/// one.
/// </summary>
internal static class OperationalTestData
{
	public static readonly DateTime CreationTime = new(2026, 7, 1, 12, 0, 0, DateTimeKind.Utc);

	public static RequestedResources NewRequestedResources()
	{
		var identityScope = new IdentityScope(
			ScopeVisibility.Public, "profile", "Profile", "Profile", ["name", "family_name"])
		{
			// The constructor derives Description from DisplayName; setting it apart proves the round-trip
			// reproduces what was persisted instead of re-deriving it.
			Description = "Profile description",
			Required = true,
			Emphasize = true,
			Enabled = true,
			ShowInDiscoveryDocument = false,
		};

		var scope = new Scope(ScopeVisibility.Internal, "api.read", "Read", "Read description")
		{
			Required = true,
			Emphasize = true,
			Enabled = false,
			ShowInDiscoveryDocument = true,
		};

		var protectedResource = new ProtectedResource("https://api.example/orders")
		{
			ShowInDiscoveryDocument = false,
			DisplayName = "Orders",
			DocumentationUri = "https://docs.example",
			PolicyUri = "https://policy.example",
			TosUri = "https://tos.example",
		};

		var resourceServer = new ResourceServer(ScopeVisibility.Public, "api", "API", "API description")
		{
			Audience = "https://api.example",
			AllowScopeRequests = false,
			Enabled = true,
			ShowInDiscoveryDocument = true,
			Scopes = [scope],
			ProtectedResources = [protectedResource],
			AllowedAccessTokenSigningAlgorithms = ["RS256", "ES256"],
		};

		var resources = new RequestedResources([identityScope], [resourceServer], [scope])
		{
			OfflineAccess = true,
		};

		resources.RequestedScopeNames.Add("openid");
		resources.RequestedScopeNames.Add("api.read");
		resources.MissingScopes.Add("api.write");
		resources.RequestedResourceUris.Add("https://api.example/orders");
		resources.InvalidTargets.Add("https://unknown.example");
		resources.ProtectedResources.Add(protectedResource);

		return resources;
	}

	public static AccessToken NewReferenceAccessToken(string jti = "at-jti-1")
	{
		var token = new AccessToken(
			"client-one",
			"https://issuer.example",
			AccessTokenType.Reference,
			CreationTime,
			3600,
			jti,
			"Bearer")
		{
			RealmId = "realm-a",
			Confirmation = "cnf-value",
			Audiences = ["https://api.example", "https://other.example"],
			AllowedSigningAlgorithms = ["RS256"],
		};

		token.ResourceUris.Add("https://api.example/orders");
		token.Claims.Add(new Claim("sub", "subject-one"));
		token.Claims.Add(new Claim("sid", "session-one"));
		token.Claims.Add(new Claim("auth_time", "1780000000", ClaimValueTypes.Integer64));

		return token;
	}

	public static RefreshToken NewRefreshToken(string handle = "rt-handle-1")
	{
		var token = new RefreshToken(
			"subject-one",
			"session-one",
			"at-jti-1",
			["openid", "api.read"],
			"client-one",
			"https://issuer.example",
			CreationTime,
			86400,
			handle)
		{
			RealmId = "realm-a",
			Confirmation = "cnf-value",
			Audiences = ["https://api.example"],
			AllowedSigningAlgorithms = ["RS256"],
		};

		token.ResourceUris.Add("https://api.example/orders");
		token.Claims.Add(new Claim("amr", "pwd"));

		return token;
	}

	public static AuthorizationCode NewAuthorizationCode()
	{
		var identity = new ClaimsIdentity(
			[
				new Claim("sub", "subject-one"),
				new Claim("sid", "session-one"),
				new Claim("auth_time", "1780000000", ClaimValueTypes.Integer64),
			],
			"RoyalIdentity",
			"sub",
			"role");

		return new AuthorizationCode(
			"client-one",
			new ClaimsPrincipal(identity),
			"session-state",
			CreationTime,
			300,
			NewRequestedResources(),
			"https://client.example/callback")
		{
			RealmId = "realm-a",
			Nonce = "nonce-value",
			StateHash = "state-hash",
			SessionId = "session-one",
			CodeChallenge = "challenge",
			CodeChallengeMethod = "S256",
			Properties = new Dictionary<string, string> { ["custom"] = "value" },
		};
	}

	public static Consent NewConsent()
	{
		var consent = new Consent
		{
			RealmId = "realm-a",
			SubjectId = "subject-one",
			ClientId = "client-one",
			CreationTime = CreationTime,
			Expiration = CreationTime.AddDays(30),
		};

		consent.AddScopes(
		[
			new ConsentedScope
			{
				Scope = "Api.Read",
				Description = "Read",
				CreationTime = CreationTime,
				JustOnce = false,
			},
			new ConsentedScope
			{
				Scope = "api.read",
				Description = "read",
				CreationTime = CreationTime,
				JustOnce = true,
			},
		]);

		return consent;
	}

	public static NameValueCollection NewAuthorizeParameters()
	{
		var parameters = new NameValueCollection
		{
			{ "client_id", "client-one" },
			{ "response_type", "code" },
			{ "scope", "openid profile" },
		};

		// Repeated key: the authorize endpoint accepts more than one `resource` (RFC 8707).
		parameters.Add("resource", "https://api.example/orders");
		parameters.Add("resource", "https://api.example/invoices");

		return parameters;
	}
}
