using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RoyalIdentity.Endpoints.Abstractions;
using RoyalIdentity.Endpoints.Mapping;
using RoyalIdentity.Pipelines.Abstractions;
using RoyalIdentity.Pipelines.Configurations;

namespace RoyalIdentity.Pipelines.Infrastructure;

public static class Pipe
{
    public static IServiceCollection AddPipelines(
        this IServiceCollection services,
        Action<IPipelineConfigurationBuilder> configureAction)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configureAction);

        services.TryAddTransient<IPipelineDispatcher, PipelineDispatcher>();
        services.TryAddTransient(typeof(PipelineDispatcher<>));

        var builder = new DefaultPipelineConfigurationBuilder(services);
        configureAction(builder);

        builder.Complete();

        return services;
    }

    public static RouteHandlerBuilder MapPipeline<TEndpoint>(this IEndpointRouteBuilder builder, string pattern)
        where TEndpoint : class, IEndpointHandler
    {
        return builder.Map(pattern, DefaultServerEndpoints.MapServerEndpoint<TEndpoint>);
    }

    public static RouteHandlerBuilder MapPipeline<TEndpoint>(this RouteGroupBuilder builder, string pattern)
        where TEndpoint : class, IEndpointHandler
    {
        return builder.Map(pattern, DefaultServerEndpoints.MapServerEndpoint<TEndpoint>);
    }

    public static RouteHandlerBuilder MapPipelineGet<TEndpoint>(this IEndpointRouteBuilder builder, string pattern)
        where TEndpoint : class, IEndpointHandler
    {
        return builder.MapGet(pattern, DefaultServerEndpoints.MapServerEndpoint<TEndpoint>);
    }

    public static RouteHandlerBuilder MapPipelineGet<TEndpoint>(this RouteGroupBuilder builder, string pattern)
        where TEndpoint : class, IEndpointHandler
    {
        return builder.MapGet(pattern, DefaultServerEndpoints.MapServerEndpoint<TEndpoint>);
    }

    public static RouteHandlerBuilder MapPipelinePost<TEndpoint>(this IEndpointRouteBuilder builder, string pattern)
        where TEndpoint : class, IEndpointHandler
    {
        return builder.MapPost(pattern, DefaultServerEndpoints.MapServerEndpoint<TEndpoint>);
    }

    public static RouteHandlerBuilder MapPipelinePost<TEndpoint>(this RouteGroupBuilder builder, string pattern)
        where TEndpoint : class, IEndpointHandler
    {
        return builder.MapPost(pattern, DefaultServerEndpoints.MapServerEndpoint<TEndpoint>);
    }

    public static void Demo(IServiceCollection services)
    {
        services.AddPipelines(b =>
        {
            b.For<CodeTokenEndpointContext>()
                .UseValidator<CodeTokenEndpointParametersValidator>()
                .UseDecorator<ClientIdentifierDecorator>()
                .UseHandler<CodeTokenEndpointHandler>();

        });
    }
}

file class TokenEndpointContextBase : AbstractContextBase
{
    public TokenEndpointContextBase(HttpContext httpContext, ContextItems? items = null)
        : base(httpContext, items)
    { }
}

file class CodeTokenEndpointContext : TokenEndpointContextBase
{
    public CodeTokenEndpointContext(HttpContext httpContext, ContextItems? items = null) 
        : base(httpContext, items)
    { }
}

file class CodeTokenEndpointParametersValidator : IValidator<CodeTokenEndpointContext>
{
    public ValueTask Validate(CodeTokenEndpointContext context, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}

file class ClientIdentifierDecorator : IDecorator<TokenEndpointContextBase>
{
    public ValueTask Decorate(TokenEndpointContextBase context, Func<ValueTask> next, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}

file class CodeTokenEndpointHandler : IHandler<CodeTokenEndpointContext>
{
    public ValueTask Handle(CodeTokenEndpointContext context, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}