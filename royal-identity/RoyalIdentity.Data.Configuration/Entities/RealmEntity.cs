namespace RoyalIdentity.Data.Configuration.Entities;

/// <summary>
/// Realm row (table <c>realms</c>): relational identity/binding fields plus the versioned
/// <c>RealmOptions</c> JSON payload (plan DF5). Deletion is a permanent tombstone (plan DF22).
/// </summary>
public class RealmEntity
{
	public required string Id { get; set; }

	public required string Path { get; set; }

	/// <summary>Canonical lowercase domain — normalization happens at the write edges (plan DF23).</summary>
	public required string Domain { get; set; }

	public required string DisplayName { get; set; }

	public bool Enabled { get; set; }

	public bool Internal { get; set; }

	public int OptionsVersion { get; set; }

	public required string OptionsJson { get; set; }

	/// <summary>Permanent tombstone marker (plan DF22); null while the realm is live.</summary>
	public DateTime? DeletedAtUtc { get; set; }
}
