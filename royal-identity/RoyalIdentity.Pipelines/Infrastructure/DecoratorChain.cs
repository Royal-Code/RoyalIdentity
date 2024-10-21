using RoyalIdentity.Endpoints.Abstractions;
using RoyalIdentity.Pipelines.Abstractions;

namespace RoyalIdentity.Pipelines.Infrastructure;

internal sealed class DecoratorChain<TContext, TDecorator, TChain> : IContextPipeline<TContext>
    where TContext : class, IContextBase
    where TDecorator : class, IDecorator<TContext>
    where TChain : IContextPipeline<TContext>
{
    private readonly TDecorator decorator;
    private readonly TChain next;

    public DecoratorChain(TDecorator decorator, TChain next)
    {
        this.decorator = decorator;
        this.next = next;
    }

    public async Task SendAsync(TContext context, CancellationToken ct)
    {
        await decorator.Decorate(context, () => next.SendAsync(context, ct), ct);
    }
}