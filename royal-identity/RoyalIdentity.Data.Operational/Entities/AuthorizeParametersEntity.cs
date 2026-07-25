namespace RoyalIdentity.Data.Operational.Entities;

/// <summary>
/// Authorize-parameters row (table <c>authorize_parameters</c>, plan DF16): the server-side continuation of an
/// authorize request across the login and consent screens. It is realm-bound, located by the domain-separated
/// digest of its handle (plan DF38) and carries an absolute expiration written at store time; reading an
/// expired row is fail-closed. This is not a PAR store and not an <c>IMessageStore</c> (plan DF26).
/// </summary>
public class AuthorizeParametersEntity
{
	/// <summary>The realm this continuation belongs to. A logical link by value (plan DF6).</summary>
	public required string RealmId { get; set; }

	/// <summary>Domain-separated SHA-256 digest of the handle; the raw handle is never persisted (plan DF38).</summary>
	public required string HandleDigest { get; set; }

	public DateTime CreatedAtUtc { get; set; }

	/// <summary>
	/// Absolute expiration computed at write time from the realm's interaction lifetime. Changing the option
	/// later never reinterprets rows that already exist (plan DF16).
	/// </summary>
	public DateTime ExpiresAtUtc { get; set; }

	public int PayloadVersion { get; set; }

	/// <summary>The versioned payload envelope holding the authorize parameters (plan DF30).</summary>
	public required string ProtectedPayload { get; set; }
}
