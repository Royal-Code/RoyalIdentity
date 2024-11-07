using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RoyalIdentity.Contexts.Decorators;
using RoyalIdentity.Contexts.Validators;
using RoyalIdentity.Contracts;
using RoyalIdentity.Contracts.Defaults;
using RoyalIdentity.Contracts.Defaults.Jobs;
using RoyalIdentity.Endpoints;
using RoyalIdentity.Handlers;
using RoyalIdentity.Users;
using RoyalIdentity.Users.Contracts;
using RoyalIdentity.Users.Defaults;
using RoyalIdentity.Utils.Caching;

namespace RoyalIdentity.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddOpenIdConnectProviderServices(
        this IServiceCollection services,
        Action<CustomOptions>? customization = null)
    {
        services.AddHttpContextAccessor();

        // Default Pipelines
        services.AddRoyalIdentityPipelines(customization);

        // jobs
        services.AddTransient<IHostedService, DefaultServerJobsStartup>();
        services.AddTransient<IServerJob, FirstKeyJob>();

        // Default contract implementations
        services.AddTransient<IAuthorizeRequestValidator, DefaultAuthorizeRequestValidator>();
        services.AddTransient<ICodeFactory, DefaultCodeFactory>();
        services.AddTransient<IConsentService, DefaultConsentService>();
        services.AddTransient<IEventDispatcher, DefaultEventDispatcher>();
        services.AddTransient(typeof(DefaultEventDispatcher<>));
        services.AddTransient<IJwtFactory, DefaultJwtFactory>();
        services.AddTransient<IKeyManager, DefaultKeyManager>();
        services.AddTransient<IProfileService, DefaultProfileService>();
        services.AddTransient<IRedirectUriValidator, DefaultRedirectUriValidator>();
        services.AddTransient<ISessionStateGenerator, DefaultSessionStateGenerator>();
        services.AddTransient<ITokenClaimsService, DefaultTokenClaimsService>();
        services.AddTransient<ITokenFactory, DefaultTokenFactory>();
        services.AddTransient<IExtensionsGrantsProvider, DefaultExtensionsGrantsProvider>();
        services.AddSingleton<IClientSecretChecker, DefaultClientSecretChecker>();

        // Default Users Services
        services.AddScoped<ISignInManager, DefaultSignInManager>();
        services.AddScoped<IUserSession, DefaultUserSession>();
        services.AddScoped<ISignInManager, DefaultSignInManager>();
        services.AddScoped<IPasswordProtector, DefaultPasswordProtector>();

        // Decorators
        services.AddTransient<ConsentDecorator>();
        services.AddTransient<LoadClient>();
        services.AddTransient<ProcessRequestObject>();
        services.AddTransient<PromptLoginDecorator>();
        services.AddTransient<StateHashDecorator>();

        // Validators
        services.AddTransient<AuthorizeMainValidator>();
        services.AddTransient<PkceValidator>();
        services.AddTransient<RedirectUriValidator>();
        services.AddTransient<RequestedResourcesValidator>();

        // Handlers
        services.AddTransient<DiscoveryHandler>();
        services.AddTransient<JwkHandler>();
        services.AddTransient<AuthorizeContextHandler>();

        // Endpoints
        services.AddTransient<DiscoveryEndpoint>();
        services.AddTransient<JwkEndpoint>();
        services.AddTransient<AuthorizeEndpoint>();

        // Others
        services.AddSingleton<KeyCache>();

        return services;
    }
}
