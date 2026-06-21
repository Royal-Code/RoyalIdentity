using Microsoft.EntityFrameworkCore;
using RoyalCode.SmartProblems;
using RoyalIdentity.UserAccounts.Features.ScopeProperties.Domain;
using RoyalIdentity.UserAccounts.Infrastructure.Data;

namespace RoyalIdentity.UserAccounts.Features.ScopeProperties.Commons;

/// <summary>
/// Persistence-aware guard for the schema rule deferred from persistence: a property definition's value type
/// cannot change while persisted values of a different type already exist. Changing it would leave stored values
/// inconsistent with the active schema, so an explicit migration is required first.
/// <para>
/// This is the building block for the (future) schema approval/edit use case; it is exposed as a service so that
/// flow can enforce the rule with a single, tested check.
/// </para>
/// </summary>
public sealed class PropertyValueTypeChangeGuard(UserAccountsDbContext db)
{
	/// <summary>
	/// Ensures a value type change is allowed for a stable property definition.
	/// </summary>
	/// <param name="propertyDefinitionId">The stable property definition identifier.</param>
	/// <param name="newValueType">The value type the schema would change to.</param>
	/// <param name="ct">A cancellation token.</param>
	/// <returns>A failure when persisted values of a different type exist; success otherwise.</returns>
	public async Task<Result> EnsureValueTypeChangeAllowedAsync(
		long propertyDefinitionId,
		PropertyValueType newValueType,
		CancellationToken ct = default)
	{
		var hasConflictingValues = await db.Set<UserAccountPropertyValue>()
			.AnyAsync(v => v.PropertyDefinitionId == propertyDefinitionId && v.ValueType != newValueType, ct);

		if (hasConflictingValues)
		{
			return Problems.InvalidState(
				"Cannot change the property value type while values of a different type already exist; an explicit migration is required.",
				nameof(PropertyDefinitionVersion.ValueType),
				"user_account.property_value_type_change_blocked");
		}

		return Result.Ok();
	}
}
