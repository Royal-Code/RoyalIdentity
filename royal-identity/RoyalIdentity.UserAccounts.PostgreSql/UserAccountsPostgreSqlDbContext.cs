using Microsoft.EntityFrameworkCore;
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
		modelBuilder.ApplyUserAccountsPostgreSqlMappings();
	}
}
