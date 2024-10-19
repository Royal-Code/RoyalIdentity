using Microsoft.Extensions.DependencyInjection;
using RoyalIdentity.Contexts;
using RoyalIdentity.Contexts.Decorators;
using RoyalIdentity.Contexts.Validators;
using RoyalIdentity.Handlers;
using RoyalIdentity.Pipelines.Configurations;
using RoyalIdentity.Pipelines.Infrastructure;

namespace RoyalIdentity;

public static class Pipes
{

    public static void AddRoyalIdentityPipelines(this IServiceCollection services, Action<CustomOptions>? customization)
    {
        var options = new CustomOptions();
        customization?.Invoke(options);

        services.AddPipelines(builder =>
        {
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


        });
    }
}

public class CustomOptions
{
    public Action<IPipelineConfigurationBuilder<AuthorizeContext>>? CustomizeAuthorizeContext { get; set; }
}