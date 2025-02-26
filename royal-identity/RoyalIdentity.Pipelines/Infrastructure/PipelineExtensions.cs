using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RoyalIdentity.Endpoints.Abstractions;
using RoyalIdentity.Endpoints.Defaults;
using RoyalIdentity.Endpoints.Mapping;
using RoyalIdentity.Pipelines.Configurations;

namespace RoyalIdentity.Pipelines.Infrastructure;

public static class PipelineExtensions
{
    public static IServiceCollection AddPipelines(
        this IServiceCollection services,
        Action<IPipelineConfigurationBuilder> configureAction)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configureAction);

        services.TryAddTransient<IPipelineDispatcher, PipelineDispatcher>();
        services.TryAddTransient(typeof(PipelineDispatcher<>));
        services.TryAddTransient(typeof(HandlerChain<,>));
        services.TryAddTransient(typeof(DecoratorChain<,,>));
        services.TryAddTransient(typeof(ValidatorChain<,,>));
        services.TryAddSingleton(typeof(OkHandler<>));

        var builder = new DefaultPipelineConfigurationBuilder(services);
        configureAction(builder);

        builder.Complete();

        return services;
    }

    public static RouteHandlerBuilder MapPipeline<TEndpoint>(this IEndpointRouteBuilder builder, string pattern)
        where TEndpoint : class, IEndpointHandler
    {
        return builder.Map(pattern.WithRealm(), ServerEndpoint<TEndpoint>.EndpointHandler);
    }

    public static RouteHandlerBuilder MapPipeline<TEndpoint>(this RouteGroupBuilder builder, string pattern)
        where TEndpoint : class, IEndpointHandler
    {
        return builder.Map(pattern.WithRealm(), ServerEndpoint<TEndpoint>.EndpointHandler);
    }

    public static RouteHandlerBuilder MapPipelineGet<TEndpoint>(this IEndpointRouteBuilder builder, string pattern)
        where TEndpoint : class, IEndpointHandler
    {
        return builder.MapGet(pattern.WithRealm(), ServerEndpoint<TEndpoint>.EndpointHandler);
    }

    public static RouteHandlerBuilder MapPipelineGet<TEndpoint>(this RouteGroupBuilder builder, string pattern)
        where TEndpoint : class, IEndpointHandler
    {
        return builder.MapGet(pattern.WithRealm(), ServerEndpoint<TEndpoint>.EndpointHandler);
    }

    public static RouteHandlerBuilder MapPipelinePost<TEndpoint>(this IEndpointRouteBuilder builder, string pattern)
        where TEndpoint : class, IEndpointHandler
    {
        return builder.MapPost(pattern.WithRealm(), ServerEndpoint<TEndpoint>.EndpointHandler);
    }

    public static RouteHandlerBuilder MapPipelinePost<TEndpoint>(this RouteGroupBuilder builder, string pattern)
        where TEndpoint : class, IEndpointHandler
    {
        return builder.MapPost(pattern.WithRealm(), ServerEndpoint<TEndpoint>.EndpointHandler);
    }
        
    private static string WithRealm(this string pattern) => $"{{realm}}/{pattern}";
}