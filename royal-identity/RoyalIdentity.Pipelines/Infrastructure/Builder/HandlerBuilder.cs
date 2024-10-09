using RoyalIdentity.Endpoints.Abstractions;
using RoyalIdentity.Pipelines.Abstractions;

namespace RoyalIdentity.Pipelines.Infrastructure.Builder;

internal class HandlerBuilder<TContext, THandler> : ChainBuilder<TContext>
    where TContext : class, IContextBase
    where THandler : class, IHandler<TContext>
{
    public override Type Build()
    {
        return Previous?.Build<HandlerChain<TContext, THandler>>()
               ?? typeof(HandlerChain<TContext, THandler>);
    }

    internal override Type Build<T>()
    {
        throw new NotSupportedException();
    }
}