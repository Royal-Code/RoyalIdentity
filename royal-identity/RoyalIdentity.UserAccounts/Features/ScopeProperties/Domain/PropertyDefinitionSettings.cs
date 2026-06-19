namespace RoyalIdentity.UserAccounts.Features.ScopeProperties.Domain;

/// <summary>
/// Settings used to create or update a property definition version.
/// </summary>
public class PropertyDefinitionSettings
{
	/// <summary>
	/// Gets or sets the value type.
	/// </summary>
	public PropertyValueType ValueType { get; set; } = PropertyValueType.Text;

	/// <summary>
	/// Gets or sets the display name.
	/// </summary>
	public string? DisplayName { get; set; }

	/// <summary>
	/// Gets or sets the help text.
	/// </summary>
	public string? Help { get; set; }

	/// <summary>
	/// Gets or sets whether the value is sensitive.
	/// </summary>
	public bool IsSensitive { get; set; }

	/// <summary>
	/// Gets or sets whether the value is required for new writes.
	/// </summary>
	public bool IsRequired { get; set; }

	/// <summary>
	/// Gets or sets whether the property accepts multiple values.
	/// </summary>
	public bool IsCollection { get; set; }

	/// <summary>
	/// Gets or sets validation rules.
	/// </summary>
	public PropertyValidationRules ValidationRules { get; set; } = PropertyValidationRules.None;

	/// <summary>
	/// Gets or sets whether this definition participates in projection and writes.
	/// </summary>
	public bool IsActive { get; set; } = true;
}
