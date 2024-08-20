using RoyalIdentity.Endpoints.Abstractions;
using RoyalIdentity.Pipelines.Abstractions;

namespace RoyalIdentity.Pipelines.Infrastructure;

internal sealed class HandlerChain<TContext, THandler> : IContextPipeline<TContext>
    where TContext : class, IContextBase
    where THandler : class, IHandler<TContext>
{
    private readonly THandler handler;

    public HandlerChain(THandler handler)
    {
        this.handler = handler;
    }

    public async Task SendAsync(TContext context, CancellationToken ct)
    {
        await handler.Handle(context, ct);
    }
}
