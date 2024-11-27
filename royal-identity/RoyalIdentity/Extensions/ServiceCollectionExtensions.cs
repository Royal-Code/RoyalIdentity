using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RoyalIdentity.Contexts.Decorators;
using RoyalIdentity.Contexts.Validators;
using RoyalIdentity.Contracts;
using RoyalIdentity.Contracts.Defaults;
using RoyalIdentity.Contracts.Defaults.Jobs;
using RoyalIdentity.Contracts.Defaults.SecretsEvaluators;
using RoyalIdentity.Contracts.Storage;
using RoyalIdentity.Endpoints;
using RoyalIdentity.Handlers;
using RoyalIdentity.Options;
using RoyalIdentity.Users;
using RoyalIdentity.Users.Contracts;
using RoyalIdentity.Users.Defaults;
using RoyalIdentity.Utils;
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
        services.AddTransient<IBackChannelLogoutNotifier, DefaultBackChannelLogoutNotifier>();
        services.AddTransient<IBearerTokenLocator, DefaultBearerTokenLocator>();
        services.AddSingleton<IClientSecretChecker, DefaultClientSecretChecker>();
        services.AddTransient<ICodeFactory, DefaultCodeFactory>();
        services.AddTransient<IConsentService, DefaultConsentService>();
        services.AddTransient<IEventDispatcher, DefaultEventDispatcher>();
        services.AddTransient(typeof(DefaultEventDispatcher<>));
        services.AddTransient<IExtensionsGrantsProvider, DefaultExtensionsGrantsProvider>();
        services.AddTransient<IJwtFactory, DefaultJwtFactory>();
        services.AddTransient<IKeyManager, DefaultKeyManager>();
        services.AddTransient<IProfileService, DefaultProfileService>();
        services.AddTransient<IRedirectUriValidator, DefaultRedirectUriValidator>();
        services.AddTransient<IReplayCache, DefaultReplayNoCache>();
        services.AddTransient<ISessionStateGenerator, DefaultSessionStateGenerator>();
        services.AddTransient<ITokenClaimsService, DefaultTokenClaimsService>();
        services.AddTransient<ITokenFactory, DefaultTokenFactory>();
        services.AddTransient<ITokenValidator, DefaultTokenValidator>();
        services.AddSingleton<IMessageStore, ProtectedDataMessageStore>();

        // Secret Evaluators
        services.AddTransient<IClientSecretsEvaluator, BasicSecretEvaluator>();
        services.AddTransient<IClientSecretsEvaluator, PostBodySecretEvaluator>();
        services.AddTransient<IClientSecretsEvaluator, PrivateKeyJwtSecretEvaluator>();
        services.AddTransient<IClientSecretsEvaluator, TlsClientAuthSecretEvaluator>();
        services.AddTransient<IClientSecretsEvaluator, NoSecretEvaluator>();

        // Default Users Services
        services.AddScoped<ISignInManager, DefaultSignInManager>();
        services.AddScoped<IUserSession, DefaultUserSession>();
        services.AddScoped<ISignInManager, DefaultSignInManager>();
        services.AddScoped<ISignOutManager, DefaultSignOutManager>();
        services.AddScoped<IPasswordProtector, DefaultPasswordProtector>();

        // Decorators
        services.AddTransient<ConsentDecorator>();
        services.AddTransient<EndSessionDecorator>();
        services.AddTransient<EvaluateBearerToken>();
        services.AddTransient<EvaluateClient>();
        services.AddTransient<LoadClient>();
        services.AddTransient<LoadCode>();
        services.AddTransient<ProcessRequestObject>();
        services.AddTransient<PromptLoginDecorator>();
        services.AddTransient<StateHashDecorator>();

        // Validators
        services.AddTransient<ActiveUserValidator>();
        services.AddTransient<AuthorizeMainValidator>();
        services.AddTransient<ConsentValidator>();
        services.AddTransient<GrantTypeValidator>();
        services.AddTransient<PkceMatchValidator>();
        services.AddTransient<PkceValidator>();
        services.AddTransient<RedirectUriValidator>();
        services.AddTransient<RequestedResourcesValidator>();
        services.AddTransient<RevocationValidator>();

        // Handlers
        services.AddTransient<AuthorizationCodeHandler>();
        services.AddTransient<AuthorizeHandler>();
        services.AddTransient<DiscoveryHandler>();
        services.AddTransient<EndSessionHandler>();
        services.AddTransient<JwkHandler>();
        services.AddTransient<RevocationHandler>();
        services.AddTransient<UserInfoHandler>();

        // Endpoints
        services.AddTransient<AuthorizeCallbackEndpoint>();
        services.AddTransient<AuthorizeEndpoint>();
        services.AddTransient<CheckSessionEndpoint>();
        services.AddTransient<DiscoveryEndpoint>();
        services.AddTransient<EndSessionEndpoint>();
        services.AddTransient<JwkEndpoint>();
        services.AddTransient<RevocationEndpoint>();
        services.AddTransient<TokenEndpoint>();
        services.AddTransient<UserInfoEndpoint>();

        // Others
        services.AddSingleton<KeyCache>();
        services.AddTransient<JwtUtil>();
        services.AddHttpClient(ServerConstants.HttpClients.BackChannelLogoutHttpClient)
            .ConfigureHttpClient(http => http.Timeout = TimeSpan.FromSeconds(ServerConstants.HttpClients.DefaultTimeoutSeconds))
            .AddPolicyHandler(ServerConstants.HttpClients.GetRetryPolicy())
            .AddPolicyHandler(ServerConstants.HttpClients.GetCircuitBreakerPolicy());

        return services;
    }
}
