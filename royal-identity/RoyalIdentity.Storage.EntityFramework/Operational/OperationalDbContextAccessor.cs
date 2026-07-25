using Microsoft.EntityFrameworkCore;

namespace RoyalIdentity.Storage.EntityFramework.Operational;

internal sealed class OperationalDbContextAccessor<TContext>(TContext dbContext) : IOperationalDbContextAccessor
	where TContext : DbContext
{
	public DbContext DbContext { get; } = dbContext;
}
