
using RoyalIdentity.Endpoints.Abstractions;

namespace RoyalIdentity.Pipelines.Abstractions;

public interface IDecorator<TContext>
    where TContext : IContextBase
{
    ValueTask Decorate(TContext context, Func<TContext, ValueTask> next, CancellationToken cancellationToken);
}