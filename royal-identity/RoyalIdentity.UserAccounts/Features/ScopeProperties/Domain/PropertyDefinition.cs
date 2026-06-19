using RoyalCode.Entities;

namespace RoyalIdentity.UserAccounts.Features.ScopeProperties.Domain;

/// <summary>
/// Stable identity for a dynamic property claim type inside a realm.
/// </summary>
public class PropertyDefinition : Entity<long>
{
#nullable disable
	/// <summary>
	/// Constructor for EF Core.
	/// </summary>
	protected PropertyDefinition()
	{
	}
#nullable restore

	internal PropertyDefinition(string realmId, PropertyScope propertyScope, string claimType)
	{
		RealmId = realmId;
		PropertyScopeId = propertyScope.Id;
		PropertyScope = propertyScope;
		ClaimType = claimType;
	}

	/// <summary>
	/// Gets the realm that owns this property definition.
	/// </summary>
	public string RealmId { get; private set; } = string.Empty;

	/// <summary>
	/// Gets the owning property scope foreign key.
	/// </summary>
	public long PropertyScopeId { get; private set; }

	/// <summary>
	/// Gets the owning property scope.
	/// </summary>
	public virtual PropertyScope? PropertyScope { get; private set; }

	/// <summary>
	/// Gets the immutable claim type represented by this definition.
	/// </summary>
	public string ClaimType { get; private set; } = string.Empty;

	internal void AttachTo(PropertyScope propertyScope)
	{
		RealmId = propertyScope.RealmId;
		PropertyScopeId = propertyScope.Id;
		PropertyScope = propertyScope;
	}
}
