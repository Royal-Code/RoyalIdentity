using RoyalIdentity.Endpoints.Abstractions;
using RoyalIdentity.Pipelines.Abstractions;

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