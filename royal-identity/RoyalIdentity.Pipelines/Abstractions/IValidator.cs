
using RoyalIdentity.Endpoints.Abstractions;

namespace RoyalIdentity.Pipelines.Abstractions;

public interface IValidator<TContext>
    where TContext : IContextBase
{
    ValueTask Validate(TContext context, CancellationToken cancellationToken);
}