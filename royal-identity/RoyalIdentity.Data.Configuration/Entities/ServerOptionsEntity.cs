namespace RoyalIdentity.Data.Configuration.Entities;

/// <summary>
/// Single authoritative row of server-wide options (table <c>server_options</c>): a versioned JSON payload
/// whose serialization/materialization belongs to the adapter (plan DF4/DF5). The table holds exactly one
/// row, enforced by <see cref="SingletonId"/> plus a check constraint.
/// </summary>
public class ServerOptionsEntity
{
	/// <summary>The only valid value of <see cref="Id"/>.</summary>
	public const short SingletonId = 1;

	public short Id { get; set; } = SingletonId;

	public int PayloadVersion { get; set; }

	public required string PayloadJson { get; set; }

	public DateTime UpdatedAtUtc { get; set; }
}
