namespace RoyalIdentity.Data.Configuration.Entities;

/// <summary>
/// Client root (table <c>clients</c>), realm-bound by composite key. Fase 1 carries the identity and the
/// first scalars; the complete scalar inventory of the core <c>Client</c> is mapped in Fase 2, guarded by a
/// property-coverage test.
/// </summary>
public class ClientEntity
{
	public required string RealmId { get; set; }

	public required string ClientId { get; set; }

	public required string Name { get; set; }

	public bool Enabled { get; set; }
}
