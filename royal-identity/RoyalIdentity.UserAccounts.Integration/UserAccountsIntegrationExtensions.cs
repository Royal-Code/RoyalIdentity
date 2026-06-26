using Microsoft.Extensions.DependencyInjection.Extensions;
using RoyalIdentity.UserAccounts.Features.Accounts.Domain;
using RoyalIdentity.UserAccounts.Infrastructure.Audit;
using RoyalIdentity.UserAccounts.Integration;
using RoyalIdentity.Users.Contracts;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Opt-in wiring that points the IdP edge ports at the UserAccounts module (ADR-015 §2.1). Call after the module
/// persistence/provider is registered (e.g. <c>AddUserAccountsSqlite(...)</c> / <c>AddUserAccountsPostgreSql(...)</c>)
/// and after the IdP storage (which registers the in-memory fake <see cref="IUserDirectory"/>): this replaces that
/// fake with the module-backed gateway.
/// </summary>
public static class UserAccountsIntegrationExtensions
{
    /// <summary>
    /// Registers the integration adapter: the realm-boundary translation, the password hashing bridge and the
    /// module-backed <see cref="IUserDirectory"/> (replacing the in-memory fake).
    /// </summary>
    /// <param name="services">The service collection (module persistence already configured).</param>
    /// <returns>The same collection for chaining.</returns>
    public static IServiceCollection AddUserAccountsForRoyalIdentity(this IServiceCollection services)
    {
        // Realm boundary translation (Realm -> RealmId + UserAccountsRealmOptions). TryAdd so a host can supply a
        // persistent options resolver before calling this (the default returns module defaults per realm).
        services.TryAddSingleton<IUserAccountsRealmOptionsResolver, DefaultUserAccountsRealmOptionsResolver>();
        services.TryAddTransient<UserAccountsRealmBindingFactory>();

        // The module's host-provided hashing seam, bridged to the IdP password protector so module-created and
        // module-authenticated credentials use the host's configured hashing.
        services.TryAddTransient<IUserAccountPasswordHasher, PasswordProtectorAccountHasher>();

        // Opt-in swap: replace the in-memory fake gateway with the module-backed one. Scoped, because it consumes
        // the module's scoped collaborators from the request scope.
        services.Replace(ServiceDescriptor.Scoped<IUserDirectory, UserAccountsUserDirectory>());

        // Bridge from the module's per-trigger invalidation policy (Q7) to the IdP's active revocation (Q13).
        services.TryAddScoped<SessionInvalidationExecutor>();

        // Make security auditing realm-aware: read the enabled categories from the realm's account options (Q8),
        // replacing the module default (all-on for every realm).
        services.Replace(ServiceDescriptor.Singleton<ISecurityAuditPolicyProvider, RealmSecurityAuditPolicyProvider>());

        return services;
    }
}
