namespace RoyalIdentity.Data.Configuration.Entities;

/// <summary>
/// Client claim row (table <c>client_claims</c>). <see cref="Ordinal"/> is assigned by the writer to keep
/// a stable identity within the client; it is not a database-generated value.
/// </summary>
public class ClientClaimEntity
{
	public required string RealmId { get; set; }

	public required string ClientId { get; set; }

	public int Ordinal { get; set; }

	public required string Type { get; set; }

	public required string Value { get; set; }

	public string? ValueType { get; set; }

	public string? Issuer { get; set; }

	public string? OriginalIssuer { get; set; }
}
