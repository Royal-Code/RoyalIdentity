namespace RoyalIdentity.Data.Configuration.Entities;

/// <summary>
/// Client secret row (table <c>client_secrets</c>). <see cref="Value"/> stores the already-hashed/encoded
/// secret exactly as the core models it; the adapter never logs it (plan invariante 10).
/// </summary>
public class ClientSecretEntity
{
	public required string RealmId { get; set; }

	public required string ClientId { get; set; }

	public int Ordinal { get; set; }

	public required string Type { get; set; }

	public required string Value { get; set; }

	public string? Description { get; set; }

	public DateTime? ExpirationUtc { get; set; }
}
