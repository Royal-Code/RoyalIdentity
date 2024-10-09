using RoyalIdentity.Endpoints.Abstractions;
using RoyalIdentity.Pipelines.Abstractions;

namespace RoyalIdentity.Pipelines.Infrastructure.Builder;

internal class DecoratorBuilder<TContext, TDecorator> : ChainBuilder<TContext>
    where TContext : class, IContextBase
    where TDecorator : class, IDecorator<TContext>
{
    public override Type Build()
    {
        throw new NotSupportedException();
    }

    internal override Type Build<T>()
    {
        return Previous?.Build<DecoratorChain<TContext, TDecorator, T>>()
               ?? typeof(DecoratorChain<TContext, TDecorator, T>);
    }
}