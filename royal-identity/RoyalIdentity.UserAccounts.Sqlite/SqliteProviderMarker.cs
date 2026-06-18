namespace RoyalIdentity.UserAccounts.Sqlite;

/// <summary>
/// Assembly anchor for the SQLite provider of the <c>UserAccounts</c> module (ADR-015 §2.1).
/// Carries mappings/indexes/migrations for SQLite in later phases (and backs tests with an in-memory
/// connection); references the pure module only.
/// </summary>
public sealed class SqliteProviderMarker
{
	/// <summary>Anchor to the pure module assembly, enforced by architecture tests.</summary>
	public static readonly System.Type ModuleAnchor = typeof(RoyalIdentity.UserAccounts.UserAccountsAssemblyMarker);
}
