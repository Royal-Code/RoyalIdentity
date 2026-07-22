using Microsoft.EntityFrameworkCore;

namespace RoyalIdentity.Storage.EntityFramework.Configuration;

/// <summary>
/// Scoped seam giving the Configuration stores access to the consumer-chosen <see cref="DbContext"/>
/// (plan DF3/DF6): stores work over <see cref="DbContext.Set{TEntity}()"/> and never require the concrete
/// default context, so a third-party combined context satisfies the same registration.
/// </summary>
public interface IConfigurationDbContextAccessor
{
	DbContext DbContext { get; }
}
