using Microsoft.EntityFrameworkCore;
using RoyalIdentity.UserAccounts.Infrastructure.Data;
using RoyalIdentity.UserAccounts.Infrastructure.Events;

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
	/// <param name="dispatcher">The domain event dispatcher (post-commit).</param>
	public UserAccountsPostgreSqlDbContext(
		DbContextOptions<UserAccountsPostgreSqlDbContext> options, IDomainEventDispatcher dispatcher)
		: base(options, dispatcher)
	{
	}

	/// <inheritdoc />
	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		base.OnModelCreating(modelBuilder);
		modelBuilder.ApplyUserAccountsPostgreSqlMappings();
	}
}
