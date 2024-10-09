using Microsoft.Extensions.DependencyInjection;
using RoyalIdentity.Endpoints.Abstractions;

namespace RoyalIdentity.Pipelines.Infrastructure;

internal sealed class PipelineDispatcher : IPipelineDispatcher
{
    private readonly IServiceProvider sp;

    public PipelineDispatcher(IServiceProvider sp)
    {
        this.sp = sp;
    }

    public Task SendAsync(IContextBase context, CancellationToken ct)
    {
        var contextType = context.GetType();
        var pipeType = typeof(PipelineDispatcher<>).MakeGenericType(contextType);
        var pipe = (IPipelineDispatcher)sp.GetRequiredService(pipeType);
        return pipe.SendAsync(context, ct);
    }
}

internal sealed class PipelineDispatcher<TContext> : IPipelineDispatcher
    where TContext : class, IContextBase
{
    private readonly IContextPipeline<TContext> pipeline;

    public PipelineDispatcher(IContextPipeline<TContext> pipeline)
    {
        this.pipeline = pipeline;
    }

    public Task SendAsync(IContextBase context, CancellationToken ct)
    {
        return pipeline.SendAsync((TContext)context, ct);
    }
}