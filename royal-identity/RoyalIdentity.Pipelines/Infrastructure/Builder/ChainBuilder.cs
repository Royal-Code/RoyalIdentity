using RoyalIdentity.Endpoints.Abstractions;

namespace RoyalIdentity.Pipelines.Infrastructure.Builder;

internal abstract class ChainBuilder<TContext>
    where TContext : class, IContextBase
{
    protected ChainBuilder<TContext>? Previous { get; set; }

    public ChainBuilder<TContext> Next(ChainBuilder<TContext> next)
    {
        next.Previous = this;
        return next;
    }

    public abstract Type Build();

    internal abstract Type Build<T>()
        where T : class, IContextPipeline<TContext>;
}