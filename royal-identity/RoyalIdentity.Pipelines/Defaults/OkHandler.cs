using RoyalIdentity.Pipelines.Abstractions;

namespace RoyalIdentity.Pipelines.Defaults;

public class OkHandler<TContext> : IHandler<TContext>
    where TContext : IContextBase
{
    public Task Handle(TContext context, CancellationToken ct)
    {
        context.Response = ResponseHandler.Ok();
        return Task.CompletedTask;
    }
}
