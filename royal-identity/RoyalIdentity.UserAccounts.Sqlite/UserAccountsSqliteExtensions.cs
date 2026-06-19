using RoyalCode.WorkContext.EntityFramework.Configurations;
using RoyalIdentity.UserAccounts.Infrastructure.Data;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// SQLite provider wiring for the UserAccounts module persistence.
/// </summary>
public static class UserAccountsSqliteExtensions
{
	/// <summary>
	/// Registers the UserAccounts module backed by a SQLite database whose connection string is read
	/// from configuration by name.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="connectionStringName">The configuration key of the connection string.</param>
	/// <returns>The WorkContext builder for further configuration (e.g. seeding).</returns>
	public static IWorkContextBuilder<UserAccountsDbContext> AddUserAccountsSqlite(
		this IServiceCollection services,
		string connectionStringName = "UserAccounts")
	{
		return services
			.AddSqliteWorkContext<UserAccountsDbContext>(connectionStringName)
			.ConfigureUserAccounts();
	}

	/// <summary>
	/// Registers the UserAccounts module backed by an in-memory SQLite connection and creates the schema.
	/// Intended for tests and ephemeral scenarios.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <returns>The WorkContext builder for further configuration (e.g. seeding).</returns>
	public static IWorkContextBuilder<UserAccountsDbContext> AddUserAccountsSqliteInMemory(
		this IServiceCollection services)
	{
		return services
			.AddSqliteInMemoryWorkContext<UserAccountsDbContext>()
			.ConfigureUserAccounts()
			.EnsureDatabaseCreated();
	}
}
