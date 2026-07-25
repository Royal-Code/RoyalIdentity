using RoyalIdentity.Models.Tokens;

namespace RoyalIdentity.Storage.EntityFramework.Operational.Materialization.Payloads;

/// <summary>
/// The persisted graph of a <see cref="RefreshToken"/>. The raw handle is never here — it is the lookup
/// argument, whose digest is the key (plan DF38) — and neither is any identifier of the previous access
/// token: the row of that access token is not a dependency of a refresh (plan DF41).
/// </summary>
public sealed class RefreshTokenPayload
{
	public required string ClientId { get; set; }

	public required string Issuer { get; set; }

	public DateTime CreationTime { get; set; }

	public int Lifetime { get; set; }

	public string? RealmId { get; set; }

	public string? Confirmation { get; set; }

	public List<string> RequestedScopes { get; set; } = [];

	public List<string> ResourceUris { get; set; } = [];

	public List<string> Audiences { get; set; } = [];

	public List<string> AllowedSigningAlgorithms { get; set; } = [];

	public List<ClaimPayload> Claims { get; set; } = [];
}
