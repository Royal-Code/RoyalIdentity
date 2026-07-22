namespace RoyalIdentity.Data.Configuration.Entities;

/// <summary>
/// Single-string collection value of a client (table <c>client_string_values</c>): URIs, grant/response
/// types, scopes/resources, algorithms and restrictions, discriminated by <see cref="Kind"/>.
/// <see cref="ComparisonKey"/> carries the per-kind uniqueness key (e.g. case-insensitive CORS origins)
/// decided by the adapter, never by provider collation (plan DF5 and baseline DF18).
/// </summary>
public class ClientStringValueEntity
{
	public required string RealmId { get; set; }

	public required string ClientId { get; set; }

	public required string Kind { get; set; }

	public required string Value { get; set; }

	public required string ComparisonKey { get; set; }
}
