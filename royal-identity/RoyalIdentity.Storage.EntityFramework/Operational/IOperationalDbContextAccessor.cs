using Microsoft.EntityFrameworkCore;

namespace RoyalIdentity.Storage.EntityFramework.Operational;

/// <summary>
/// Scoped seam giving the Operational stores access to the consumer-chosen <see cref="DbContext"/>
/// (plan DF1/DF2): stores work over <see cref="DbContext.Set{TEntity}()"/> and never require the concrete
/// default context, so a third-party context — including one combining Configuration and Operational —
/// satisfies the same registration. It is deliberately separate from the Configuration accessor: the two
/// families may resolve to different contexts, connections and even databases (plan DF2/DF6).
/// </summary>
public interface IOperationalDbContextAccessor
{
	DbContext DbContext { get; }
}
