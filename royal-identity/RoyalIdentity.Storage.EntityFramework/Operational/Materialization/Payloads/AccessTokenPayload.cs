using RoyalIdentity.Models.Tokens;

namespace RoyalIdentity.Storage.EntityFramework.Operational.Materialization.Payloads;

/// <summary>
/// The persisted graph of an <see cref="AccessToken"/>. Queryable fields (subject, client, session, type,
/// timestamps) live in relational columns; everything else is here.
/// <para>
/// <see cref="Token"/> is written only when the token string differs from the <c>jti</c> — that is, for a JWT
/// persisted in <c>Full</c> mode. A reference token's bearer coincides with its <c>jti</c>, which is the
/// lookup argument, so it is never copied into the payload (plan DF13/DF38).
/// </para>
/// </summary>
public sealed class AccessTokenPayload
{
	public required string ClientId { get; set; }

	public required string Issuer { get; set; }

	public required string TokenType { get; set; }

	public AccessTokenType AccessTokenType { get; set; }

	public DateTime CreationTime { get; set; }

	public int Lifetime { get; set; }

	public string? RealmId { get; set; }

	public string? Confirmation { get; set; }

	/// <summary>The compact JWT, present only in <c>Full</c> mode; <c>null</c> otherwise.</summary>
	public string? Token { get; set; }

	public List<string> Audiences { get; set; } = [];

	public List<string> AllowedSigningAlgorithms { get; set; } = [];

	public List<string> ResourceUris { get; set; } = [];

	public List<ClaimPayload> Claims { get; set; } = [];
}
