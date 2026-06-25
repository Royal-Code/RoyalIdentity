using Microsoft.EntityFrameworkCore;
using RoyalIdentity.UserAccounts.Features.Accounts.Domain;

namespace RoyalIdentity.UserAccounts.Sqlite;

/// <summary>
/// EF Core model configuration for SQLite-specific UserAccounts storage details.
/// </summary>
public static class UserAccountsSqliteModelBuilderExtensions
{
	/// <summary>
	/// Applies SQLite-specific mappings for the UserAccounts module.
	/// </summary>
	/// <param name="modelBuilder">The EF Core model builder.</param>
	/// <returns>The same model builder for additional configuration.</returns>
	public static ModelBuilder ApplyUserAccountsSqliteMappings(this ModelBuilder modelBuilder)
	{
		ArgumentNullException.ThrowIfNull(modelBuilder);

		// At most one primary email per account, enforced at the database level.
		modelBuilder.Entity<UserAccountEmail>()
			.HasIndex(e => new { e.RealmId, e.UserAccountId })
			.IsUnique()
			.HasFilter("\"IsPrimary\" = 1")
			.HasDatabaseName("UX_UserAccountEmails_PrimaryPerAccount");

		// At most one primary phone per account, enforced at the database level.
		modelBuilder.Entity<UserAccountPhone>()
			.HasIndex(p => new { p.RealmId, p.UserAccountId })
			.IsUnique()
			.HasFilter("\"IsPrimary\" = 1")
			.HasDatabaseName("UX_UserAccountPhones_PrimaryPerAccount");

		return modelBuilder;
	}
}
