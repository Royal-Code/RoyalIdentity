namespace RoyalIdentity.Pipelines.Abstractions;

public interface IDecorator<in TContext>
{
    ValueTask Decorate(TContext context, Func<ValueTask> next, CancellationToken ct);
}