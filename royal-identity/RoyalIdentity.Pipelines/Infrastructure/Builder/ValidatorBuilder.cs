using RoyalIdentity.Endpoints.Abstractions;
using RoyalIdentity.Pipelines.Abstractions;

namespace RoyalIdentity.Pipelines.Infrastructure.Builder;

internal class ValidatorBuilder<TContext, TValidator> : ChainBuilder<TContext>
    where TContext : class, IContextBase
    where TValidator : class, IValidator<TContext>
{
    public override Type Build()
    {
        throw new NotSupportedException();
    }

    internal override Type Build<T>()
    {
        return Previous?.Build<ValidatorChain<TContext, TValidator, T>>()
               ?? typeof(ValidatorChain<TContext, TValidator, T>);
    }
}