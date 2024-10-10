namespace RoyalIdentity.Pipelines.Abstractions;

public interface IHandler<in TContext>
{
    ValueTask Handle(TContext context, CancellationToken cancellationToken);
}