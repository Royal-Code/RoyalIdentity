using Microsoft.Extensions.DependencyInjection;
using RoyalIdentity.Contracts.Storage;
using RoyalIdentity.Users.Contracts;

namespace RoyalIdentity.Storage.InMemory.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInMemoryStorage(this IServiceCollection services)
    {
        services.AddSingleton(new MemoryStorage());

        services.AddTransient<IAccessTokenStore, AccessTokenStore>();
        services.AddTransient<IAuthorizationCodeStore, AuthorizationCodeStore>();
        services.AddTransient<IAuthorizeParametersStore, AuthorizeParametersStore>();
        services.AddTransient<IClientStore, ClientStore>();
        services.AddTransient<IResourceStore, ResourceStore>();
        services.AddTransient<IUserConsentStore, UserConsentStore>();
        services.AddTransient<IUserStore, UserStore>();
        services.AddTransient<IUserDetailsStore, UserStore>();
        services.AddTransient<IUserSessionStore, UserSessionStore>();
        services.AddTransient<IKeyStore, KeyStore>();

        return services;
    }
}
