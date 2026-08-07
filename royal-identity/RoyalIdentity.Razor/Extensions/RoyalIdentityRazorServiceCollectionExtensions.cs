using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RoyalIdentity.Configuration;
using RoyalIdentity.Contracts.Localization;
using RoyalIdentity.Razor.Localization;
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

        // ResourcesPath must stay "Resources" and the marker types must stay in the assembly root namespace:
        // together they resolve to RoyalIdentity.Razor.Resources.<Marker>, which is where the .resx live.
        services.AddLocalization(options => options.ResourcesPath = "Resources");

        // Validation messages and field display names come from the shared catalogue through the .NET 10
        // validation pipeline; no experimental DataAnnotations-for-Blazor package (DF18).
        services.AddValidation();

        // Replacing the core's empty catalogue must not depend on which Add* the host calls first. Removing
        // only the known empty default keeps a host's own catalogue intact, while TryAdd alone would leave the
        // empty one in place whenever AddOpenIdConnectProviderServices() ran first — a composition whose
        // behaviour silently changed with call order.
        var emptyDefault = services.FirstOrDefault(descriptor =>
            descriptor.ServiceType == typeof(IUiLocaleCatalog)
            && descriptor.ImplementationType == typeof(EmptyUiLocaleCatalog));

        if (emptyDefault is not null)
            services.Remove(emptyDefault);

        services.TryAddSingleton<IUiLocaleCatalog, ResxUiLocaleCatalog>();
        services.AddScoped<IConfigurationSnapshotValidator, UiLocaleConfigurationValidator>();

        services.AddHttpContextAccessor();
        services.AddScoped<ISessionContextService, SessionContextService>();
        services.AddScoped<ILoginPageService, LoginPageService>();
        services.AddScoped<IConsentPageService, ConsentPageService>();
        services.AddScoped<IEndSessionPageService, EndSessionPageService>();

        return services;
    }
}