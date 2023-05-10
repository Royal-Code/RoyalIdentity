
namespace RoyalIdentity.Pipelines.Abstractions;

public interface IHandler<TContext>
{
    ValueTask Handle(TContext context, CancellationToken cancellationToken);
}