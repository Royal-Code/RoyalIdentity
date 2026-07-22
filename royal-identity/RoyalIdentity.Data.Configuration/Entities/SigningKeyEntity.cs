namespace RoyalIdentity.Data.Configuration.Entities;

/// <summary>
/// Signing key row (table <c>signing_keys</c>): queryable metadata in relational columns and the key
/// material as an opaque payload protected by an <c>IKeyMaterialProtector</c> chosen at composition
/// (plan DF8/DF24). Create-only: rows are never overwritten.
/// </summary>
public class SigningKeyEntity
{
	public required string RealmId { get; set; }

	public required string KeyId { get; set; }

	public required string Name { get; set; }

	public required string SecurityAlgorithm { get; set; }

	public int SerializationFormat { get; set; }

	public int Encoding { get; set; }

	public DateTime CreatedUtc { get; set; }

	public DateTime? NotBeforeUtc { get; set; }

	public DateTime? ExpiresUtc { get; set; }

	/// <summary>Identifier of the protector that produced <see cref="ProtectedMaterial"/> (plan DF8).</summary>
	public required string ProtectorId { get; set; }

	public required string ProtectedMaterial { get; set; }
}
