namespace RoyalIdentity.Pipelines.Abstractions;

public interface IHandler<in TContext>
{
    Task Handle(TContext context, CancellationToken ct);
}