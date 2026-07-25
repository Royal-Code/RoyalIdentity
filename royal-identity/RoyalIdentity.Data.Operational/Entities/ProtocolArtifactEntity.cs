namespace RoyalIdentity.Data.Operational.Entities;

/// <summary>
/// Shared, discriminated row of the protocol artifacts that share one principal key, one realm, one
/// expiration and a compatible lifecycle (table <c>protocol_artifacts</c>, plan DF36): reference access
/// tokens, refresh tokens, authorization codes, and JWT access-token metadata/full when the realm enables it.
/// <para>
/// The identity is <c>(RealmId, ArtifactType, LookupDigest)</c>: the raw bearer/opaque handle is never
/// persisted, only its domain-separated SHA-256 digest (plan DF38). Queryable columns stay in the clear;
/// everything else lives in the versioned, realm-profile-protected payload (plan DF30). Columns that only
/// apply to one artifact type are nullable by design — the typed stores are the authority on which ones they
/// write, and <see cref="ArtifactType"/> is part of every key, query, mutation and cleanup predicate.
/// </para>
/// </summary>
public class ProtocolArtifactEntity
{
	/// <summary>The realm this artifact belongs to. A logical link by value: there is no cross-family FK (plan DF6).</summary>
	public required string RealmId { get; set; }

	/// <summary>The discriminator; see <see cref="ProtocolArtifactTypes"/>.</summary>
	public required string ArtifactType { get; set; }

	/// <summary>Domain-separated SHA-256 digest of the artifact's lookup key (plan DF38).</summary>
	public required string LookupDigest { get; set; }

	/// <summary>The subject that owns the artifact, when the artifact has one (client credentials do not).</summary>
	public string? SubjectId { get; set; }

	/// <summary>The client the artifact was issued to. A logical link by value (plan DF6).</summary>
	public required string ClientId { get; set; }

	/// <summary>The SSO session the artifact belongs to, when there is one. A logical, indexed link (plan DF35).</summary>
	public string? SessionId { get; set; }

	/// <summary>Redirect URI bound to an authorization code; <c>null</c> for the other artifact types.</summary>
	public string? RedirectUri { get; set; }

	/// <summary>Access-token type (JWT/Reference) for <see cref="ProtocolArtifactTypes.AccessToken"/> rows.</summary>
	public int? AccessTokenType { get; set; }

	public DateTime CreatedAtUtc { get; set; }

	/// <summary>Absolute expiration, persisted so validity never depends on the configuration in force at read time.</summary>
	public DateTime ExpiresAtUtc { get; set; }

	/// <summary>When a refresh token was consumed; <c>null</c> while it has not been (plan DF12).</summary>
	public DateTime? ConsumedAtUtc { get; set; }

	/// <summary>
	/// State version of a refresh token, used by the conditional transition (plan DF12). Other artifact types
	/// keep it at its initial value and never compare it.
	/// </summary>
	public int StateVersion { get; set; }

	/// <summary>
	/// Claims mode captured when a refresh token was issued (plan DF32), so changing the realm option later
	/// never reinterprets tokens that already exist. <c>null</c> for the other artifact types.
	/// </summary>
	public int? ClaimsMode { get; set; }

	/// <summary>
	/// Version of <see cref="ProtectedPayload"/>. <c>null</c> when the artifact keeps no payload at all — for
	/// example a JWT access token persisted as metadata only (plan DF31).
	/// </summary>
	public int? PayloadVersion { get; set; }

	/// <summary>The versioned payload envelope produced by the realm's protection profile (plan DF30).</summary>
	public string? ProtectedPayload { get; set; }
}
