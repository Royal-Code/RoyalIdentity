using RoyalIdentity.Endpoints.Abstractions;
using RoyalIdentity.Pipelines.Abstractions;

namespace RoyalIdentity.Pipelines.Infrastructure;

internal sealed class ValidatorChain<TContext, TValidator, TChain> : IContextPipeline<TContext>
    where TContext : class, IContextBase
    where TValidator : class, IValidator<TContext>
    where TChain : IContextPipeline<TContext>
{
    private readonly TValidator validator;
    private readonly TChain next;

    public ValidatorChain(TValidator validator, TChain next)
    {
        this.validator = validator;
        this.next = next;
    }

    public async Task SendAsync(TContext context, CancellationToken ct)
    {
        await validator.Validate(context, ct);

        if (context.Response is not null)
            await next.SendAsync(context, ct);
    }
}
