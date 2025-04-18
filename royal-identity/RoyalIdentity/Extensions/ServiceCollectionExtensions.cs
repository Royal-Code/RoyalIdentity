﻿using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RoyalIdentity.Authentication;
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
        services.AddRoyalIdentityAuthentication();

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
        services.AddTransient<IClientSecretEvaluator, BasicSecretEvaluator>();
        services.AddTransient<IClientSecretEvaluator, PostBodySecretEvaluator>();
        services.AddTransient<IClientSecretEvaluator, PrivateKeyJwtSecretEvaluator>();
        services.AddTransient<IClientSecretEvaluator, TlsClientAuthSecretEvaluator>();
        services.AddTransient<IClientSecretEvaluator, NoSecretEvaluator>();

        // Default Users Services
        services.AddScoped<ISignInManager, DefaultSignInManager>();
        services.AddScoped<ISignOutManager, DefaultSignOutManager>();
        services.AddTransient<IPasswordProtector, DefaultPasswordProtector>();

        // Decorators
        services.AddTransient<ClientResourceDecorator>();
        services.AddTransient<ConsentDecorator>();
        services.AddTransient<EndSessionDecorator>();
        services.AddTransient<EvaluateBearerToken>();
        services.AddTransient<EvaluateClient>();
        services.AddTransient<LoadClient>();
        services.AddTransient<LoadCode>();
        services.AddTransient<LoadRefreshToken>();
        services.AddTransient<ProcessRequestObject>();
        services.AddTransient<PromptLoginDecorator>();
        services.AddTransient<ResourcesDecorator>();
        services.AddTransient<StateHashDecorator>();

        // Validators
        services.AddTransient<ActiveUserValidator>();
        services.AddTransient<AuthorizationResourcesValidator>();
        services.AddTransient<AuthorizeMainValidator>();
        services.AddTransient<EndSessionValidator>();
        services.AddTransient<GrantTypeValidator>();
        services.AddTransient<PkceMatchValidator>();
        services.AddTransient<PkceValidator>();
        services.AddTransient<RedirectUriValidator>();
        services.AddTransient<ResourcesValidator>();
        services.AddTransient<RevocationValidator>();

        // Handlers
        services.AddTransient<AuthorizationCodeHandler>();
        services.AddTransient<AuthorizeHandler>();
        services.AddTransient<ClientCredentialsHandler>();
        services.AddTransient<DiscoveryHandler>();
        services.AddTransient<EndSessionHandler>();
        services.AddTransient<JwkHandler>();
        services.AddTransient<RefreshTokenHandler>();
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
        services.AddSingleton<RealmCaching>();
        services.AddTransient<JwtUtil>();
        services.AddHttpClient(ServerConstants.HttpClients.BackChannelLogoutHttpClient)
            .ConfigureHttpClient(http => http.Timeout = TimeSpan.FromSeconds(ServerConstants.HttpClients.DefaultTimeoutSeconds))
            .AddPolicyHandler(ServerConstants.HttpClients.GetRetryPolicy())
            .AddPolicyHandler(ServerConstants.HttpClients.GetCircuitBreakerPolicy());

        return services;
    }

    private static void AddRoyalIdentityAuthentication(this IServiceCollection services)
    {
        services.AddAuthentication(options =>
            {
                options.DefaultScheme = Server.AuthenticationScheme;
            })
            .AddCookie(Server.DefaultCookieAuthenticationScheme)
            .AddPolicyScheme(Server.AuthenticationScheme, Server.AuthenticationName, options =>
            {
                options.ForwardDefaultSelector = context =>
                {
                    // try get the realm from the route
                    if (context.Request.RouteValues.TryGetValue(Server.RealmRouteKey, out var realm))
                    {
                        return $"{Server.RealmAuthenticationNamePrefix}{realm}";
                    }
                    // try get realm from context items
                    else if (context.Items.TryGetValue(Server.RealmRouteKey, out var item) && item is string realmItem)
                    {
                        return $"{Server.RealmAuthenticationNamePrefix}{realmItem}";
                    }
                    // else, use cookie authentication
                    else
                    {
                        return Server.DefaultCookieAuthenticationScheme;
                    }
                };
            });

        // Substituir o AuthenticationSchemeProvider para suportar *realms* dinâmicos
        services.AddSingleton<IAuthenticationSchemeProvider, RealmsAuthenticationSchemeProvider>();

        // Adicionar configuração dinâmica de cookies
        services.ConfigureOptions<ConfigureRealmCookieAuthenticationOptions>();

        // authorization
        services.AddAuthorization();
    }
}
