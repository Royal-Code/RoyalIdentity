using Microsoft.EntityFrameworkCore;
using RoyalCode.WorkContext.EntityFramework.Configurations;

namespace RoyalIdentity.UserAccounts.Infrastructure.Data;

/// <summary>
/// Registers the UserAccounts module persistence (repositories and searches) on a WorkContext builder.
/// Provider projects call this after wiring their EF Core provider connection.
/// </summary>
public static class UserAccountsWorkContextExtensions
{
	/// <summary>
	/// Configures repositories and searches for the UserAccounts module aggregates.
	/// </summary>
	/// <typeparam name="TDbContext">The module DbContext type.</typeparam>
	/// <param name="builder">The WorkContext builder.</param>
	/// <returns>The same builder for chaining.</returns>
	public static IWorkContextBuilder<TDbContext> ConfigureUserAccounts<TDbContext>(
		this IWorkContextBuilder<TDbContext> builder)
		where TDbContext : DbContext
	{
		var moduleAssembly = typeof(UserAccountsAssemblyMarker).Assembly;

		builder.ConfigureRepositories(moduleAssembly);
		builder.ConfigureSearches(moduleAssembly);

		return builder;
	}
}
