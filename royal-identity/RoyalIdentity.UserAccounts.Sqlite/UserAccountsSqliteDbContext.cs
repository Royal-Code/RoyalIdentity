using Microsoft.EntityFrameworkCore;
using RoyalIdentity.UserAccounts.Infrastructure.Data;

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
	public UserAccountsSqliteDbContext(DbContextOptions<UserAccountsSqliteDbContext> options)
		: base(options)
	{
	}

	/// <inheritdoc />
	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		base.OnModelCreating(modelBuilder);
		modelBuilder.ApplyUserAccountsSqliteMappings();
	}
}
