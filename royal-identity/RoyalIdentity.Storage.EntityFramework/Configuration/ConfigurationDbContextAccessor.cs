using Microsoft.EntityFrameworkCore;

namespace RoyalIdentity.Storage.EntityFramework.Configuration;

internal sealed class ConfigurationDbContextAccessor<TContext>(TContext dbContext) : IConfigurationDbContextAccessor
	where TContext : DbContext
{
	public DbContext DbContext { get; } = dbContext;
}
