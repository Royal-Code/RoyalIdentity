using Microsoft.EntityFrameworkCore;

namespace RoyalIdentity.UserAccounts.Infrastructure.Data;

/// <summary>
/// EF Core model configuration for the provider-agnostic UserAccounts mappings.
/// </summary>
public static class UserAccountsModelBuilderExtensions
{
	/// <summary>
	/// Applies the provider-agnostic UserAccounts entity mappings.
	/// </summary>
	/// <param name="modelBuilder">The EF Core model builder.</param>
	/// <returns>The same model builder for provider-specific configuration.</returns>
	public static ModelBuilder ApplyUserAccountsMappings(this ModelBuilder modelBuilder)
	{
		ArgumentNullException.ThrowIfNull(modelBuilder);

		modelBuilder.ApplyConfigurationsFromAssembly(typeof(UserAccountsDbContext).Assembly);
		return modelBuilder;
	}
}
