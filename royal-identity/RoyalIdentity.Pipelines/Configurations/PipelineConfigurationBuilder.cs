using Microsoft.Extensions.DependencyInjection;
using RoyalIdentity.Endpoints.Abstractions;
using RoyalIdentity.Pipelines.Abstractions;
using RoyalIdentity.Pipelines.Infrastructure.Builder;

namespace RoyalIdentity.Pipelines.Configurations;

public sealed class PipelineConfigurationBuilder<TContext>(IServiceCollection services)
    : IPipelineConfigurationBuilder<TContext>, ICompletable
    where TContext : class, IContextBase
{
    private ChainBuilder<TContext>? chainBuilder;

    public IPipelineConfigurationBuilder<TContext> UseDecorator<TDecorator>() where TDecorator : class, IDecorator<TContext>
    {
        var decorator = new DecoratorBuilder<TContext, TDecorator>();
        chainBuilder = chainBuilder?.Next(decorator) ?? decorator;
        return this;
    }

    public IPipelineConfigurationBuilder<TContext> UseValidator<TValidator>() where TValidator : class, IValidator<TContext>
    {
        var validator = new ValidatorBuilder<TContext, TValidator>();
        chainBuilder = chainBuilder?.Next(validator) ?? validator;
        return this;
    }

    public void UseHandler<THandler>() where THandler : class, IHandler<TContext>
    {
        var handler = new HandlerBuilder<TContext, THandler>();
        chainBuilder = chainBuilder?.Next(handler) ?? handler;
    }

    public void Complete()
    {
        if (chainBuilder is null)
            throw new InvalidOperationException($"No handler configured for the {typeof(TContext).Name} context pipeline");

        var chainType = chainBuilder.Build();
        var pipeType = typeof(IContextPipeline<TContext>);

        services.AddTransient(pipeType, chainType);
    }
}