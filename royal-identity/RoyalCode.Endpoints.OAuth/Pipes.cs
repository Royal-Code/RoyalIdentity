using Microsoft.Extensions.DependencyInjection;
using RoyalIdentity.Contexts;
using RoyalIdentity.Contexts.Decorators;
using RoyalIdentity.Contexts.Validators;
using RoyalIdentity.Handlers;
using RoyalIdentity.Pipelines.Infrastructure;

namespace RoyalIdentity;

public static class Pipes
{

    public static void AddRoyalIdentityPipelines(this IServiceCollection services)
    {
        services.AddPipelines(builder =>
        {
            builder.For<AuthorizeContext>()
                .UseDecorator<LoadClient>()
                .UseValidator<RedirectUriValidator>()
                .UseValidator<AuthorizeMainValidator>()
                .UseValidator<PkceValidator>()
                .UseValidator<RequestedResourcesValidator>()
                .UseDecorator<PromptLoginDecorator>()
                .UseDecorator<ConsentDecorator>()
                .UseHandler<AuthorizeContextHandler>();


        });
    }
}
