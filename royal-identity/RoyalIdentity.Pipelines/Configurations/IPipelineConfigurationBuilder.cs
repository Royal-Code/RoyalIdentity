
using RoyalIdentity.Endpoints.Abstractions;
using RoyalIdentity.Pipelines.Abstractions;

namespace RoyalIdentity.Pipelines.Configurations;

public interface IPipelineConfigurationBuilder<TContext>
    where TContext : IContextBase
{
    IPipelineConfigurationBuilder<TContext> UseDecorator<TDecorator>()
        where TDecorator : IDecorator<TContext>;

    IPipelineConfigurationBuilder<TContext> UseValidator<TValidator>()
        where TValidator : IValidator<TContext>;

    IPipelineConfigurationBuilder<TContext> UseHandler<THandler>()
        where THandler : IHandler<TContext>;

    IPipelineConfigurationBuilder<TContext> UseSwitch(Action<IPipelineSwitchBuilder<TContext>> switchBuilder);
}

public interface IPipelineSwitchBuilder<TContext>
    where TContext : IContextBase
{
    IPipelineSwitchCaseBuilder<TContext> Case(Func<TContext, bool> predicate);

    IPipelineSwitchCaseBuilder<TContext> Case<TSwitchCase>()
        where TSwitchCase : ISwitchCase<TContext>;
}

public interface IPipelineSwitchCaseBuilder<TContext>
{

}