using Microsoft.Extensions.DependencyInjection;
using RoyalIdentity.Contracts.Storage;

namespace RoyalIdentity.Storage.InMemory.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInMemoryStorage(this IServiceCollection services)
    {
        services.AddSingleton<MemoryStorage>();
        services.AddSingleton<IStorageProvider, StorageProvider>();

        services.AddTransient<IAccessTokenStore, AccessTokenStore>();
        services.AddTransient<IAuthorizationCodeStore, AuthorizationCodeStore>();
        services.AddTransient<IAuthorizeParametersStore, AuthorizeParametersStore>();
        services.AddTransient<IUserConsentStore, UserConsentStore>();
        services.AddTransient<IRefreshTokenStore, RefreshTokenStore>();

        return services;
    }
}
