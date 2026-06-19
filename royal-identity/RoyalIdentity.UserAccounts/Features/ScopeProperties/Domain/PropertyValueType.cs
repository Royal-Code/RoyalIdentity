namespace RoyalIdentity.UserAccounts.Features.ScopeProperties.Domain;

/// <summary>
/// Supported dynamic property value types.
/// </summary>
public enum PropertyValueType
{
	/// <summary>Free text value.</summary>
	Text,

	/// <summary>Signed integer value.</summary>
	Integer,

	/// <summary>Decimal number value.</summary>
	Decimal,

	/// <summary>Boolean value.</summary>
	Boolean,

	/// <summary>Date-only value.</summary>
	Date,

	/// <summary>Date and time value with offset.</summary>
	DateTime,

	/// <summary>Time-only value.</summary>
	Time
}
