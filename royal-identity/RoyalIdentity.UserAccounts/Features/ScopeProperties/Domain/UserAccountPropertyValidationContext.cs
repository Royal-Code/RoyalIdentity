namespace RoyalIdentity.UserAccounts.Features.ScopeProperties.Domain;

/// <summary>
/// Context passed to a custom dynamic property validator.
/// </summary>
public sealed class UserAccountPropertyValidationContext
{
	/// <summary>
	/// Gets the realm.
	/// </summary>
	public string RealmId { get; init; } = string.Empty;

	/// <summary>
	/// Gets the account subject identifier.
	/// </summary>
	public string SubjectId { get; init; } = string.Empty;

	/// <summary>
	/// Gets the property claim type.
	/// </summary>
	public string ClaimType { get; init; } = string.Empty;

	/// <summary>
	/// Gets the property value type.
	/// </summary>
	public PropertyValueType ValueType { get; init; }

	/// <summary>
	/// Gets the raw supplied value.
	/// </summary>
	public string RawValue { get; init; } = string.Empty;

	/// <summary>
	/// Gets the canonical value after parsing.
	/// </summary>
	public string CanonicalValue { get; init; } = string.Empty;

	/// <summary>
	/// Gets the parsed value.
	/// </summary>
	public object? ParsedValue { get; init; }

	/// <summary>
	/// Gets optional validator parameters encoded as JSON.
	/// </summary>
	public string? ParametersJson { get; init; }
}
