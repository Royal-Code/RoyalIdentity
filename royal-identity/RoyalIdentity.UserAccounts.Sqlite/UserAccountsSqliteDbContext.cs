using Microsoft.EntityFrameworkCore;
using RoyalIdentity.UserAccounts.Infrastructure.Data;
using RoyalIdentity.UserAccounts.Infrastructure.Events;

namespace RoyalIdentity.UserAccounts.Sqlite;

/// <summary>
/// SQLite-specific context for the UserAccounts module.
/// </summary>
public class UserAccountsSqliteDbContext : UserAccountsDbContext
{
	/// <summary>
	/// Creates the context.
	/// </summary>
	/// <param name="options">The context options.</param>
	/// <param name="dispatcher">The domain event dispatcher (post-commit).</param>
	public UserAccountsSqliteDbContext(
		DbContextOptions<UserAccountsSqliteDbContext> options, IDomainEventDispatcher dispatcher)
		: base(options, dispatcher)
	{
	}

	/// <inheritdoc />
	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		base.OnModelCreating(modelBuilder);
		modelBuilder.ApplyUserAccountsSqliteMappings();
	}
}
