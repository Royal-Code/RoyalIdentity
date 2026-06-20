using RoyalCode.Entities;

namespace RoyalIdentity.UserAccounts.Features.ScopeProperties.Domain;

/// <summary>
/// Versioned settings for a dynamic property definition.
/// </summary>
public class PropertyDefinitionVersion : Entity<long>
{
#nullable disable
	/// <summary>
	/// Constructor for EF Core.
	/// </summary>
	protected PropertyDefinitionVersion()
	{
	}
#nullable restore

	internal PropertyDefinitionVersion(
		string realmId,
		PropertyScopeVersion propertyScopeVersion,
		PropertyDefinition propertyDefinition,
		PropertyDefinitionSettings settings)
	{
		RealmId = realmId;
		PropertyScopeVersionId = propertyScopeVersion.Id;
		PropertyScopeVersion = propertyScopeVersion;
		PropertyDefinitionId = propertyDefinition.Id;
		PropertyDefinition = propertyDefinition;
		ClaimType = propertyDefinition.ClaimType;
		Apply(settings);
	}

	/// <summary>
	/// Gets the realm that owns this property definition version.
	/// </summary>
	public string RealmId { get; private set; } = string.Empty;

	/// <summary>
	/// Gets the owning scope version foreign key.
	/// </summary>
	public long PropertyScopeVersionId { get; private set; }

	/// <summary>
	/// Gets the owning scope version.
	/// </summary>
	public virtual PropertyScopeVersion? PropertyScopeVersion { get; private set; }

	/// <summary>
	/// Gets the stable property definition foreign key.
	/// </summary>
	public long PropertyDefinitionId { get; private set; }

	/// <summary>
	/// Gets the stable property definition.
	/// </summary>
	public virtual PropertyDefinition? PropertyDefinition { get; private set; }

	/// <summary>
	/// Gets the property claim type.
	/// </summary>
	public string ClaimType { get; private set; } = string.Empty;

	/// <summary>
	/// Gets the value type.
	/// </summary>
	public PropertyValueType ValueType { get; private set; }

	/// <summary>
	/// Gets the display name.
	/// </summary>
	public string? DisplayName { get; private set; }

	/// <summary>
	/// Gets the help text.
	/// </summary>
	public string? Help { get; private set; }

	/// <summary>
	/// Gets whether this property value is sensitive.
	/// </summary>
	public bool IsSensitive { get; private set; }

	/// <summary>
	/// Gets whether this property is required for new writes.
	/// </summary>
	public bool IsRequired { get; private set; }

	/// <summary>
	/// Gets whether this property accepts multiple values.
	/// </summary>
	public bool IsCollection { get; private set; }

	/// <summary>
	/// Gets validation rules.
	/// </summary>
	public PropertyValidationRules ValidationRules { get; private set; } = PropertyValidationRules.None;

	/// <summary>
	/// Gets whether this property participates in projection and writes.
	/// </summary>
	public bool IsActive { get; private set; }

	/// <summary>
	/// Updates versioned settings for this definition version.
	/// </summary>
	/// <param name="settings">The new settings.</param>
	internal void Update(PropertyDefinitionSettings settings)
	{
		Apply(settings);
	}

	/// <summary>
	/// Marks the property as active in this version.
	/// </summary>
	internal void Activate()
	{
		IsActive = true;
	}

	/// <summary>
	/// Marks the property as inactive in this version without deleting stored values.
	/// </summary>
	internal void Deactivate()
	{
		IsActive = false;
	}

	internal PropertyDefinitionVersion CopyTo(
		PropertyScopeVersion propertyScopeVersion,
		PropertyDefinition propertyDefinition)
	{
		var settings = new PropertyDefinitionSettings
		{
			ValueType = ValueType,
			DisplayName = DisplayName,
			Help = Help,
			IsSensitive = IsSensitive,
			IsRequired = IsRequired,
			IsCollection = IsCollection,
			ValidationRules = ValidationRules,
			IsActive = IsActive
		};

		return new PropertyDefinitionVersion(RealmId, propertyScopeVersion, propertyDefinition, settings);
	}

	internal void AttachTo(PropertyScopeVersion propertyScopeVersion)
	{
		RealmId = propertyScopeVersion.RealmId;
		PropertyScopeVersionId = propertyScopeVersion.Id;
		PropertyScopeVersion = propertyScopeVersion;
	}

	private void Apply(PropertyDefinitionSettings settings)
	{
		ValueType = settings.ValueType;
		DisplayName = settings.DisplayName;
		Help = settings.Help;
		IsSensitive = settings.IsSensitive;
		IsRequired = settings.IsRequired;
		IsCollection = settings.IsCollection;
		ValidationRules = settings.ValidationRules ?? PropertyValidationRules.None;
		IsActive = settings.IsActive;
	}
}
