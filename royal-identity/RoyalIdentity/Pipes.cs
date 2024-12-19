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
            //// Discovery Jwk
            //////////////////////////////
            var jwkPipe = builder.For<JwkContext>();

            options.CustomizeDiscoveryJsonWebKeysContext?.Invoke(jwkPipe);

            jwkPipe.UseHandler<JwkHandler>();


            //////////////////////////////
            //// AuthorizeContext
            //////////////////////////////
            var authorizeContextPipe = builder.For<AuthorizeContext>()
                .UseDecorator<ProcessRequestObject>()
                .UseDecorator<LoadClient>()
                .UseValidator<RedirectUriValidator>()
                .UseDecorator<ResourcesDecorator>()
                .UseValidator<AuthorizeMainValidator>()
                .UseValidator<PkceValidator>()
                .UseValidator<RequestedResourcesValidator>()
                .UseDecorator<PromptLoginDecorator>()
                .UseDecorator<ConsentDecorator>()
                .UseDecorator<StateHashDecorator>();

            options.CustomizeAuthorizeContext?.Invoke(authorizeContextPipe);

            authorizeContextPipe.UseHandler<AuthorizeHandler>();


            //////////////////////////////
            //// AuthorizeValidateContext
            //////////////////////////////
            var authorizeValidateContextPipe = builder.For<AuthorizeValidateContext>()
                .UseDecorator<ProcessRequestObject>()
                .UseDecorator<LoadClient>()
                .UseValidator<RedirectUriValidator>()
                .UseValidator<AuthorizeMainValidator>()
                .UseDecorator<ResourcesDecorator>()
                .UseValidator<RequestedResourcesValidator>();

            options.CustomizeAuthorizeValidateContext?.Invoke(authorizeValidateContextPipe);

            authorizeValidateContextPipe.UseHandler<OkHandler<AuthorizeValidateContext>>();


            //////////////////////////////
            //// AuthorizationCodeContext
            //////////////////////////////
            var authorizationCodeContextPipe = builder.For<AuthorizationCodeContext>()
                .UseDecorator<EvaluateClient>()
                .UseValidator<RedirectUriValidator>()
                .UseDecorator<LoadCode>()
                .UseValidator<PkceMatchValidator>()
                .UseValidator<ActiveUserValidator>();

            options.CustomizeAuthorizationCodeContext?.Invoke(authorizationCodeContextPipe);

            authorizationCodeContextPipe.UseHandler<AuthorizationCodeHandler>();


            //////////////////////////////
            //// RefreshTokenContext
            //////////////////////////////
            var refreshTokenContextPipe = builder.For<RefreshTokenContext>()
                .UseDecorator<EvaluateClient>()
                .UseDecorator<LoadRefreshToken>()
                .UseValidator<ActiveUserValidator>();

            options.CustomizeRefreshTokenContext?.Invoke(refreshTokenContextPipe);

            refreshTokenContextPipe.UseHandler<RefreshTokenHandler>();


            //////////////////////////////
            //// UserInfoContext
            //////////////////////////////
            var userinfoContextPipe = builder.For<UserInfoContext>()
                .UseDecorator<EvaluateBearerToken>();

            options.CustomizeUserInfoContext?.Invoke(userinfoContextPipe);

            userinfoContextPipe.UseHandler<UserInfoHandler>();


            //////////////////////////////
            //// RevocationInfoContext
            //////////////////////////////
            var revocationContextPipe = builder.For<RevocationContext>()
                .UseDecorator<EvaluateClient>()
                .UseValidator<RevocationValidator>();

            options.CustomizeRevocationContext?.Invoke(revocationContextPipe);

            revocationContextPipe.UseHandler<RevocationHandler>();


            //////////////////////////////
            //// EndSessionContext
            //////////////////////////////
            var endSessionContextPipe = builder.For<EndSessionContext>()
                .UseDecorator<LoadClient>()
                .UseDecorator<EndSessionDecorator>();

            options.CustomizeEndSessionContext?.Invoke(endSessionContextPipe);

            endSessionContextPipe.UseHandler<EndSessionHandler>();
        });
    }
}

public class CustomOptions
{
    public Action<IPipelineConfigurationBuilder<DiscoveryContext>>? CustomizeDiscoveryContext { get; set; }

    public Action<IPipelineConfigurationBuilder<JwkContext>>? CustomizeDiscoveryJsonWebKeysContext { get; set; }

    public Action<IPipelineConfigurationBuilder<AuthorizeContext>>? CustomizeAuthorizeContext { get; set; }

    public Action<IPipelineConfigurationBuilder<AuthorizeValidateContext>>? CustomizeAuthorizeValidateContext { get; set; }

    public Action<IPipelineConfigurationBuilder<AuthorizationCodeContext>>? CustomizeAuthorizationCodeContext { get; set; }

    public Action<IPipelineConfigurationBuilder<RefreshTokenContext>>? CustomizeRefreshTokenContext { get; set; }

    public Action<IPipelineConfigurationBuilder<UserInfoContext>>? CustomizeUserInfoContext { get; set; }

    public Action<IPipelineConfigurationBuilder<RevocationContext>>? CustomizeRevocationContext { get; set; }

    public Action<IPipelineConfigurationBuilder<EndSessionContext>>? CustomizeEndSessionContext { get; set; }
}