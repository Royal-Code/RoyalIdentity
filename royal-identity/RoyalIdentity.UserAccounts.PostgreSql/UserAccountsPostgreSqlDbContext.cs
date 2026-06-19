using Microsoft.EntityFrameworkCore;
using RoyalIdentity.UserAccounts.Features.Accounts.Domain;
using RoyalIdentity.UserAccounts.Infrastructure.Data;

namespace RoyalIdentity.UserAccounts.PostgreSql;

/// <summary>
/// PostgreSQL-specific context. It reuses the provider-agnostic model and layers PostgreSQL-only storage:
/// the <c>xmin</c> system column as the concurrency token and partial indexes.
/// </summary>
public class UserAccountsPostgreSqlDbContext : UserAccountsDbContext
{
	/// <summary>
	/// Creates the context.
	/// </summary>
	/// <param name="options">The context options.</param>
	public UserAccountsPostgreSqlDbContext(DbContextOptions<UserAccountsPostgreSqlDbContext> options)
		: base(options)
	{
	}

	/// <inheritdoc />
	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		base.OnModelCreating(modelBuilder);

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
	}
}
