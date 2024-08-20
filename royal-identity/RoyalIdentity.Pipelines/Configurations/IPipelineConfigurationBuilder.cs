using Microsoft.Extensions.DependencyInjection;
using RoyalIdentity.Endpoints.Abstractions;
using RoyalIdentity.Pipelines.Abstractions;
using RoyalIdentity.Pipelines.Infrastructure;

namespace RoyalIdentity.Pipelines.Configurations;

public interface IPipelineConfigurationBuilder
{
    IPipelineConfigurationBuilder<TContext> For<TContext>()
        where TContext : class, IContextBase;
}

public interface IPipelineConfigurationBuilder<TContext>
    where TContext : IContextBase
{
    IPipelineConfigurationBuilder<TContext> UseDecorator<TDecorator>()
        where TDecorator : class, IDecorator<TContext>;

    IPipelineConfigurationBuilder<TContext> UseValidator<TValidator>()
        where TValidator : class, IValidator<TContext>;

    void UseHandler<THandler>()
        where THandler : class, IHandler<TContext>;
}


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

public interface ICompletable
{
    void Complete();
}

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