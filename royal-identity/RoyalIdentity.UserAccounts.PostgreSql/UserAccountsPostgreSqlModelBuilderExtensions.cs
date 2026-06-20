using Microsoft.EntityFrameworkCore;
using RoyalIdentity.UserAccounts.Features.Accounts.Domain;

namespace RoyalIdentity.UserAccounts.PostgreSql;

/// <summary>
/// EF Core model configuration for PostgreSQL-specific UserAccounts storage details.
/// </summary>
public static class UserAccountsPostgreSqlModelBuilderExtensions
{
	/// <summary>
	/// Applies PostgreSQL-specific mappings for the UserAccounts module.
	/// </summary>
	/// <param name="modelBuilder">The EF Core model builder.</param>
	/// <returns>The same model builder for additional configuration.</returns>
	public static ModelBuilder ApplyUserAccountsPostgreSqlMappings(this ModelBuilder modelBuilder)
	{
		ArgumentNullException.ThrowIfNull(modelBuilder);

		// Use the PostgreSQL system column xmin as the optimistic concurrency token for the account aggregate.
		modelBuilder.Entity<UserAccount>()
			.Property(a => a.Version)
			.HasColumnName("xmin")
			.HasColumnType("xid")
			.ValueGeneratedOnAddOrUpdate()
			.IsConcurrencyToken();

		// Partial index: ExternalId lookups only target rows that actually have one.
		modelBuilder.Entity<UserAccount>()
			.HasIndex(a => new { a.RealmId, a.ExternalId })
			.HasFilter("\"ExternalId\" IS NOT NULL");

		// At most one primary email per account, enforced at the database level.
		modelBuilder.Entity<UserAccountEmail>()
			.HasIndex(e => new { e.RealmId, e.UserAccountId })
			.IsUnique()
			.HasFilter("\"IsPrimary\"")
			.HasDatabaseName("UX_UserAccountEmails_PrimaryPerAccount");

		return modelBuilder;
	}
}
