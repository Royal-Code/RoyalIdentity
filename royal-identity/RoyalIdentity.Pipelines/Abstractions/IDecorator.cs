namespace RoyalIdentity.Pipelines.Abstractions;

public interface IDecorator<in TContext>
{
    Task Decorate(TContext context, Func<Task> next, CancellationToken ct);
}