namespace RoyalIdentity.UserAccounts.Options;

/// <summary>
/// Fixed account field that can be projected as a claim.
/// </summary>
public enum FixedAccountField
{
	/// <summary>The account username.</summary>
	Username,

	/// <summary>The account display name.</summary>
	DisplayName,

	/// <summary>The primary email address.</summary>
	PrimaryEmail,

	/// <summary>Whether the primary email address is verified.</summary>
	EmailVerified,

	/// <summary>The primary phone number.</summary>
	PrimaryPhone,

	/// <summary>Whether the primary phone number is verified.</summary>
	PhoneVerified,

	/// <summary>The account roles.</summary>
	Roles,

	/// <summary>The optional external identifier.</summary>
	ExternalId
}

/// <summary>
/// Configures how a fixed account field is projected to a claim for an identity scope.
/// </summary>
public class FixedFieldClaimProjection
{
	/// <summary>
	/// Creates a new projection.
	/// </summary>
	public FixedFieldClaimProjection()
	{
	}

	/// <summary>
	/// Creates an independent copy of another projection.
	/// </summary>
	public FixedFieldClaimProjection(FixedFieldClaimProjection other)
	{
		Field = other.Field;
		ScopeName = other.ScopeName;
		ClaimType = other.ClaimType;
		Include = other.Include;
	}

	/// <summary>
	/// Gets or sets the fixed field to project.
	/// </summary>
	public FixedAccountField Field { get; set; }

	/// <summary>
	/// Gets or sets the identity scope name that enables this projection.
	/// </summary>
	public string ScopeName { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the claim type emitted for the field.
	/// </summary>
	public string ClaimType { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets whether this projection is enabled.
	/// </summary>
	public bool Include { get; set; } = true;
}
