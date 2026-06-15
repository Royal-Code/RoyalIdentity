using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server;
using RoyalIdentity.Razor.Services;

// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring RoyalIdentity Razor on a service collection.
/// </summary>
public static class RoyalIdentityRazorServiceCollectionExtensions
{
    public static IServiceCollection AddRoyalIdentityRazor(this IServiceCollection services)
    {
        // Services for the authentication server, related to the AspNetCore project and views.
        services.AddCascadingAuthenticationState();
        services.AddScoped<IdentityRedirectManager>();

        // Auth state for interactive components comes from the framework's ServerAuthenticationStateProvider
        // (reads the cookie principal). The old IdentityRevalidatingAuthenticationStateProvider was removed:
        // it resolved an unregistered IUserStore (broken) and ignored the realm. The session is already
        // validated per request by the cookie OnValidatePrincipal → IUserSessionService.IsSessionValidAsync;
        // circuit-level revalidation (SecurityStamp) is reserved out of scope (ADR-014 / plan).
        services.AddScoped<AuthenticationStateProvider, ServerAuthenticationStateProvider>();

        services.AddHttpContextAccessor();
        services.AddScoped<ISessionContextService, SessionContextService>();
        services.AddScoped<ILoginPageService, LoginPageService>();
        services.AddScoped<IConsentPageService, ConsentPageService>();
        services.AddScoped<IEndSessionPageService, EndSessionPageService>();

        return services;
    }
}