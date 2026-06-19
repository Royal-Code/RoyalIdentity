namespace RoyalIdentity.UserAccounts.Features.ScopeProperties.Domain;

/// <summary>
/// Declarative validation rules for a dynamic property.
/// </summary>
public class PropertyValidationRules
{
#nullable disable
	/// <summary>
	/// Constructor for EF Core.
	/// </summary>
	protected PropertyValidationRules()
	{
	}
#nullable restore

	/// <summary>
	/// Creates declarative validation rules.
	/// </summary>
	public PropertyValidationRules(
		int? minLength = null,
		int? maxLength = null,
		PropertyRangeRule? range = null,
		IEnumerable<string>? allowedValues = null,
		string? regexPattern = null,
		int? minItems = null,
		int? maxItems = null,
		IEnumerable<PropertyCustomValidationRef>? customValidators = null)
	{
		MinLength = minLength;
		MaxLength = maxLength;
		Range = range;
		AllowedValues = allowedValues?.ToArray() ?? [];
		RegexPattern = regexPattern;
		MinItems = minItems;
		MaxItems = maxItems;
		CustomValidators = customValidators?.ToArray() ?? [];
	}

	/// <summary>
	/// Gets a rules object with no configured rule.
	/// </summary>
	public static PropertyValidationRules None { get; } = new();

	/// <summary>
	/// Gets the optional minimum text length.
	/// </summary>
	public int? MinLength { get; private set; }

	/// <summary>
	/// Gets the optional maximum text length.
	/// </summary>
	public int? MaxLength { get; private set; }

	/// <summary>
	/// Gets the optional range rule.
	/// </summary>
	public PropertyRangeRule? Range { get; private set; }

	/// <summary>
	/// Gets the allowed canonical values.
	/// </summary>
	public IReadOnlyList<string> AllowedValues { get; private set; } = [];

	/// <summary>
	/// Gets the optional regular expression pattern.
	/// </summary>
	public string? RegexPattern { get; private set; }

	/// <summary>
	/// Gets the optional minimum collection item count.
	/// </summary>
	public int? MinItems { get; private set; }

	/// <summary>
	/// Gets the optional maximum collection item count.
	/// </summary>
	public int? MaxItems { get; private set; }

	/// <summary>
	/// Gets custom validators to invoke after declarative validation.
	/// </summary>
	public IReadOnlyList<PropertyCustomValidationRef> CustomValidators { get; private set; } = [];
}
