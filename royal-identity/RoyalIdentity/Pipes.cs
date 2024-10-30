using Microsoft.Extensions.DependencyInjection;
using RoyalIdentity.Contexts;
using RoyalIdentity.Contexts.Decorators;
using RoyalIdentity.Contexts.Validators;
using RoyalIdentity.Endpoints.Defaults;
using RoyalIdentity.Handlers;
using RoyalIdentity.Pipelines.Configurations;
using RoyalIdentity.Pipelines.Infrastructure;

namespace RoyalIdentity;

public static class Pipes
{

    public static void AddRoyalIdentityPipelines(
        this IServiceCollection services, 
        Action<CustomOptions>? customization = null)
    {
        var options = new CustomOptions();
        customization?.Invoke(options);

        services.AddPipelines(builder =>
        {
            //////////////////////////////
            //// Discovery
            //////////////////////////////
            var discoveryPipe = builder.For<DiscoveryContext>();

            options.CustomizeDiscoveryContext?.Invoke(discoveryPipe);

            discoveryPipe.UseHandler<DiscoveryHandler>();

            //////////////////////////////
            //// AuthorizeContext
            //////////////////////////////
            var authorizeContextPipe = builder.For<AuthorizeContext>()
                .UseDecorator<ProcessRequestObject>()
                .UseDecorator<LoadClient>()
                .UseValidator<RedirectUriValidator>()
                .UseValidator<AuthorizeMainValidator>()
                .UseValidator<PkceValidator>()
                .UseValidator<RequestedResourcesValidator>()
                .UseDecorator<PromptLoginDecorator>()
                .UseDecorator<ConsentDecorator>()
                .UseDecorator<StateHashDecorator>();

            options.CustomizeAuthorizeContext?.Invoke(authorizeContextPipe);

            authorizeContextPipe.UseHandler<AuthorizeContextHandler>();

            //////////////////////////////
            //// AuthorizeValidateContext
            //////////////////////////////
            var authorizeValidateContextPipe = builder.For<AuthorizeValidateContext>()
                .UseDecorator<ProcessRequestObject>()
                .UseDecorator<LoadClient>()
                .UseValidator<RedirectUriValidator>()
                .UseValidator<AuthorizeMainValidator>()
                .UseValidator<RequestedResourcesValidator>();

            options.CustomizeAuthorizeContextHandler?.Invoke(authorizeValidateContextPipe);

            authorizeValidateContextPipe.UseHandler<OkHandler<AuthorizeValidateContext>>();
        });
    }
}

public class CustomOptions
{
    public Action<IPipelineConfigurationBuilder<DiscoveryContext>>? CustomizeDiscoveryContext { get; set; }

    public Action<IPipelineConfigurationBuilder<AuthorizeContext>>? CustomizeAuthorizeContext { get; set; }

    public Action<IPipelineConfigurationBuilder<AuthorizeValidateContext>>? CustomizeAuthorizeContextHandler { get; set; }
}