
using RoyalIdentity.Endpoints.Abstractions;

namespace RoyalIdentity.Pipelines.Abstractions
{
    public interface ISwitchCase<TContext>
        where TContext : IContextBase
    {
        ValueTask<bool> Accept(TContext context, CancellationToken cancellationToken);
    }
}