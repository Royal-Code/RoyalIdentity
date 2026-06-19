using RoyalIdentity.UserAccounts.Features.ScopeProperties.Domain;
using RoyalIdentity.UserAccounts.Options;

namespace RoyalIdentity.UserAccounts.Features.ScopeProperties.Commons;

/// <summary>
/// Validates claim type uniqueness across fixed projections and dynamic property definitions.
/// </summary>
public class ValidateClaimProjectionConfiguration
{
	/// <summary>
	/// Validates fixed and dynamic claim type configuration.
	/// </summary>
	/// <param name="options">The realm options that carry fixed-field projections.</param>
	/// <param name="definitions">Dynamic property definitions.</param>
	/// <returns>A list of configuration errors. Empty means valid.</returns>
	public IReadOnlyList<string> Validate(
		UserAccountsRealmOptions options,
		IEnumerable<PropertyDefinition> definitions)
	{
		var dynamicClaimTypes = definitions
			.Select(d => d.ClaimType)
			.Where(c => !string.IsNullOrWhiteSpace(c));

		return options.Validate(dynamicClaimTypes);
	}
}
