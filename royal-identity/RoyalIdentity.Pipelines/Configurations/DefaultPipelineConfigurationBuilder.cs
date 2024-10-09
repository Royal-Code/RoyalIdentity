using Microsoft.Extensions.DependencyInjection;
using RoyalIdentity.Endpoints.Abstractions;
using RoyalIdentity.Pipelines.Infrastructure.Builder;

namespace RoyalIdentity.Pipelines.Configurations;

public sealed class DefaultPipelineConfigurationBuilder(IServiceCollection services)
    : IPipelineConfigurationBuilder
{
    private readonly LinkedList<ICompletable> completables = [];

    public IPipelineConfigurationBuilder<TContext> For<TContext>()
        where TContext : class, IContextBase
    {
        var builder = new PipelineConfigurationBuilder<TContext>(services);
        completables.AddLast(builder);
        return builder;
    }

    internal void Complete()
    {
        foreach (var item in completables)
        {
            item.Complete();
        }
    }
}