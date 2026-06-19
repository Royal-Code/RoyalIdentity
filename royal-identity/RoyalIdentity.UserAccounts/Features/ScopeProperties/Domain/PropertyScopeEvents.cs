using RoyalCode.DomainEvents;

namespace RoyalIdentity.UserAccounts.Features.ScopeProperties.Domain;

/// <summary>
/// Event raised when a property definition changes in a scope version.
/// </summary>
public class PropertyDefinitionChanged(string realmId, string scopeName, string claimType) : DomainEventBase
{
	/// <summary>
	/// Gets the realm.
	/// </summary>
	public string RealmId { get; } = realmId;

	/// <summary>
	/// Gets the scope name.
	/// </summary>
	public string ScopeName { get; } = scopeName;

	/// <summary>
	/// Gets the claim type.
	/// </summary>
	public string ClaimType { get; } = claimType;
}

/// <summary>
/// Event raised when a property scope version becomes active.
/// </summary>
public class PropertyScopeVersionActivated(string realmId, string scopeName, int version) : DomainEventBase
{
	/// <summary>
	/// Gets the realm.
	/// </summary>
	public string RealmId { get; } = realmId;

	/// <summary>
	/// Gets the scope name.
	/// </summary>
	public string ScopeName { get; } = scopeName;

	/// <summary>
	/// Gets the activated version.
	/// </summary>
	public int Version { get; } = version;
}
