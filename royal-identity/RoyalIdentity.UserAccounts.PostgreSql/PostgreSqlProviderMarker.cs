namespace RoyalIdentity.UserAccounts.PostgreSql;

/// <summary>
/// Assembly anchor for the PostgreSQL provider of the <c>UserAccounts</c> module (ADR-015 §2.1).
/// Carries mappings/indexes/migrations for PostgreSQL in later phases; references the pure module only.
/// </summary>
public sealed class PostgreSqlProviderMarker
{
	/// <summary>Anchor to the pure module assembly, enforced by architecture tests.</summary>
	public static readonly System.Type ModuleAnchor = typeof(RoyalIdentity.UserAccounts.UserAccountsAssemblyMarker);
}
