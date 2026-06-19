using RoyalCode.WorkContext.EntityFramework.Configurations;
using RoyalIdentity.UserAccounts.Infrastructure.Data;
using RoyalIdentity.UserAccounts.PostgreSql;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// PostgreSQL provider wiring for the UserAccounts module persistence.
/// </summary>
public static class UserAccountsPostgreSqlExtensions
{
	/// <summary>
	/// Registers the UserAccounts module backed by a PostgreSQL database whose connection string is read
	/// from configuration by name.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="connectionStringName">The configuration key of the connection string.</param>
	/// <returns>The WorkContext builder for further configuration (e.g. seeding).</returns>
	public static IWorkContextBuilder<UserAccountsPostgreSqlDbContext> AddUserAccountsPostgreSql(
		this IServiceCollection services,
		string connectionStringName = "UserAccounts")
	{
		return services
			.AddPostgreWorkContext<UserAccountsPostgreSqlDbContext>(connectionStringName)
			.ConfigureUserAccounts();
	}
}
