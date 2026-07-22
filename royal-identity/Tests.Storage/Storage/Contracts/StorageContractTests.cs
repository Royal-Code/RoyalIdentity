using RoyalIdentity.Models;
using RoyalIdentity.Models.Scopes;
using RoyalIdentity.Models.Tokens;
using RoyalIdentity.Security.Keys;
using RoyalIdentity.Users;
using System.Security.Claims;
using Tests.Storage.Support;

namespace Tests.Storage.Contracts;

/// <summary>
/// <para>
///     Base class of the provider-neutral storage contract suite (plan-data-storage-baseline Fase 3).
///     Each scenario class is abstract and hosts one nested concrete class per provider fixture
///     (<c>InMemory</c> today; the EF providers add their own without rewriting scenarios — DF2/DF13).
/// </para>
/// <para>
///     Scenarios only lock behaviors already decided (`preservar` lines of plan-data-storage-matrix.md) or
///     load-bearing observed behavior explicitly annotated as pending final semantics (Fase 5 / DF25).
///     Behaviors classified `substituir` (atomic code consumption, refresh-token conditional transition,
///     tombstone path/domain reservation) are acceptance requirements of Planos 2/3 and are NOT tested against
///     the transitional fake (ADR-018).
/// </para>
/// </summary>
public abstract class StorageContractTests
{
	/// <summary>Deterministic base instant, aligned with the harness clock.</summary>
	protected static readonly DateTime Start = StorageContractHarness.Start;

	protected abstract Task<StorageContractHarness> CreateHarnessAsync();

	// ─── Model builders (contract-level data; no live-reference or identity assumptions — DF17) ───

	protected static Client NewClient(Realm realm, string clientId, bool enabled = true, string? name = null) => new()
	{
		Realm = realm,
		Id = clientId,
		Name = name ?? $"Contract client {clientId}",
		Enabled = enabled,
	};

	protected static KeyParameters NewKey(
		string keyId, DateTime created, DateTime? notBefore = null, DateTime? expires = null, string? name = null)
		=> new(keyId, name ?? $"Contract key {keyId}", "RS256", KeySerializationFormat.Json, KeyEncoding.Plain,
			"{}", created)
		{
			NotBefore = notBefore,
			Expires = expires,
		};

	protected static AccessToken NewAccessToken(
		Realm realm, string jti, string clientId, string? subjectId = null,
		AccessTokenType accessTokenType = AccessTokenType.Jwt)
	{
		var token = new AccessToken(clientId, "https://issuer.contract.test", accessTokenType, Start, 3600, jti,
			"Bearer")
		{
			RealmId = realm.Id,
		};

		if (subjectId is not null)
			token.Claims.Add(new Claim("sub", subjectId));

		return token;
	}

	protected static RefreshToken NewRefreshToken(Realm realm, string handle, string subjectId, string clientId)
		=> new(subjectId, $"sid-{handle}", $"jti-{handle}", ["openid"], clientId, "https://issuer.contract.test",
			Start, 3600, handle)
		{
			RealmId = realm.Id,
		};

	protected static AuthorizationCode NewAuthorizationCode(Realm realm, string clientId, string subjectId)
	{
		var subject = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", subjectId)], "contract"));
		return new AuthorizationCode(clientId, subject, "session-state", Start, 300, new RequestedResources(),
			"https://client.contract.test/callback")
		{
			RealmId = realm.Id,
		};
	}

	protected static Consent NewConsent(Realm realm, string subjectId, string clientId, params string[] scopes)
	{
		var consent = new Consent
		{
			SubjectId = subjectId,
			ClientId = clientId,
			RealmId = realm.Id,
			CreationTime = Start,
		};
		consent.AddScopes(scopes.Select(s => new ConsentedScope { Scope = s, CreationTime = Start }));
		return consent;
	}

	protected static UserSession NewSession(string sessionId, string subjectId, bool isActive = true) => new()
	{
		Id = sessionId,
		SubjectId = subjectId,
		AuthenticationMethod = "pwd",
		IdentityProvider = "local",
		StartedAt = Start,
		LastSeenAt = Start,
		IsActive = isActive,
	};

	protected static IdentityScope NewIdentityScope(string name, bool enabled = true)
		=> new(ScopeVisibility.Public, name, $"Scope {name}", $"Contract identity scope {name}", ["sub"])
		{
			Enabled = enabled,
		};

	protected static ResourceServer NewResourceServer(string name, bool enabled = true, params Scope[] scopes)
		=> new(ScopeVisibility.Public, name, $"Server {name}", $"Contract resource server {name}")
		{
			Enabled = enabled,
			Scopes = scopes,
		};

	protected static Scope NewScope(string name, bool enabled = true)
		=> new(ScopeVisibility.Public, name, $"Scope {name}", $"Contract scope {name}")
		{
			Enabled = enabled,
		};
}
