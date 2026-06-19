using RoyalCode.Entities;
using RoyalIdentity.UserAccounts.Features.Accounts.Domain;

namespace RoyalIdentity.UserAccounts.Features.ScopeProperties.Domain;

/// <summary>
/// Dynamic property value assigned to a user account.
/// </summary>
public class UserAccountPropertyValue : Entity<long>
{
#nullable disable
	/// <summary>
	/// Constructor for EF Core.
	/// </summary>
	protected UserAccountPropertyValue()
	{
	}
#nullable restore

	internal UserAccountPropertyValue(
		UserAccount account,
		PropertyDefinition definition,
		PropertyValueType valueType,
		string value,
		int ordinal)
	{
		RealmId = account.RealmId;
		UserAccountId = account.Id;
		UserAccount = account;
		PropertyDefinitionId = definition.Id;
		PropertyDefinition = definition;
		ClaimType = definition.ClaimType;
		ValueType = valueType;
		Value = value;
		Ordinal = ordinal;
	}

	/// <summary>
	/// Gets the realm that owns this value.
	/// </summary>
	public string RealmId { get; private set; } = string.Empty;

	/// <summary>
	/// Gets the owner account foreign key.
	/// </summary>
	public long UserAccountId { get; private set; }

	/// <summary>
	/// Gets the owner account.
	/// </summary>
	public virtual UserAccount? UserAccount { get; private set; }

	/// <summary>
	/// Gets the stable property definition foreign key.
	/// </summary>
	public long PropertyDefinitionId { get; private set; }

	/// <summary>
	/// Gets the stable property definition.
	/// </summary>
	public virtual PropertyDefinition? PropertyDefinition { get; private set; }

	/// <summary>
	/// Gets the denormalized claim type.
	/// </summary>
	public string ClaimType { get; private set; } = string.Empty;

	/// <summary>
	/// Gets the canonical value.
	/// </summary>
	public string Value { get; private set; } = string.Empty;

	/// <summary>
	/// Gets the denormalized value type.
	/// </summary>
	public PropertyValueType ValueType { get; private set; }

	/// <summary>
	/// Gets the value ordinal for collection properties.
	/// </summary>
	public int Ordinal { get; private set; }
}
