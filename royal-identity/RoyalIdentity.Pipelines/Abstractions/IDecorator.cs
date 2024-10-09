
using RoyalIdentity.Endpoints.Abstractions;

namespace RoyalIdentity.Pipelines.Abstractions;

public interface IDecorator<in TContext>
{
    ValueTask Decorate(TContext context, Func<ValueTask> next, CancellationToken cancellationToken);
}