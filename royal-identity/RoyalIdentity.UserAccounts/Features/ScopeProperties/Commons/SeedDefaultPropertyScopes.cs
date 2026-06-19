using RoyalIdentity.UserAccounts.Features.ScopeProperties.Domain;

namespace RoyalIdentity.UserAccounts.Features.ScopeProperties.Commons;

/// <summary>
/// Creates the standard property scopes used by default account claim projection.
/// </summary>
public class SeedDefaultPropertyScopes
{
	/// <summary>
	/// Creates active default scopes for a realm.
	/// </summary>
	/// <param name="realmId">The realm identifier.</param>
	/// <param name="createdAt">The creation timestamp.</param>
	/// <returns>The default scopes.</returns>
	public IReadOnlyList<PropertyScope> Create(string realmId, DateTimeOffset createdAt)
	{
		var profile = CreateActiveScope(realmId, "profile", "Profile", createdAt);
		var email = CreateActiveScope(realmId, "email", "Email", createdAt);

		return [profile, email];
	}

	private static PropertyScope CreateActiveScope(
		string realmId,
		string name,
		string displayName,
		DateTimeOffset createdAt)
	{
		var scope = new PropertyScope(realmId, name, displayName, createdAt);
		scope.ApproveVersion(scope.Versions.Single(), createdAt);
		return scope;
	}
}
