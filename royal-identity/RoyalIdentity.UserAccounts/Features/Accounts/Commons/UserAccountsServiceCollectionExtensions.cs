using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RoyalIdentity.UserAccounts.Features.Accounts.Domain;
using RoyalIdentity.UserAccounts.Features.ScopeProperties.Commons;
using RoyalIdentity.UserAccounts.Features.ScopeProperties.Domain;
using RoyalIdentity.UserAccounts.Infrastructure.Audit;
using RoyalIdentity.UserAccounts.Infrastructure.Events;
using RoyalIdentity.UserAccounts.Infrastructure.Gateways;

namespace RoyalIdentity.UserAccounts.Features.Accounts.Commons;

/// <summary>
/// Registers the module's use-case collaborators. Defaults are added with <c>TryAdd</c> so a host can override
/// any single concern (normalization, subject-id generation, password hashing) before configuration.
/// </summary>
public static class UserAccountsServiceCollectionExtensions
{
	/// <summary>
	/// Registers the default UserAccounts use-case collaborators.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <returns>The same collection for chaining.</returns>
	public static IServiceCollection AddUserAccountsServices(this IServiceCollection services)
	{
		services.TryAddSingleton(TimeProvider.System);
		services.TryAddSingleton<IUserAccountNormalizer, DefaultUserAccountNormalizer>();
		services.TryAddSingleton<ISubjectIdGenerator, DefaultSubjectIdGenerator>();
		services.TryAddSingleton<PasswordPolicy>();
		services.TryAddSingleton<PasswordHistoryPolicy>();

		services.TryAddSingleton<INotificationGateway, NoopNotificationGateway>();

		// Domain event dispatch (post-commit) + security audit pipeline (Q8). Defaults are overridable: the audit
		// sink is a no-op until a host registers one, and the per-realm category policy defaults to "all on" until the
		// integration replaces it with a realm-aware provider.
		services.TryAddScoped<IDomainEventDispatcher, DomainEventDispatcher>();
		services.TryAddSingleton<ISecurityAuditSink, NoopSecurityAuditSink>();
		services.TryAddSingleton<ISecurityAuditPolicyProvider, DefaultSecurityAuditPolicyProvider>();
		services.AddScoped<IDomainEventObserver, SecurityAuditObserver>();

		services.TryAddScoped<UserAccountReader>();
		services.TryAddScoped<UserAccountActionTokenService>();
		services.TryAddScoped<UserAccountClaimProjector>();
		services.TryAddScoped<UserAccountClaimsReader>();
		services.TryAddScoped<UserAccountPropertyValueService>();
		services.TryAddScoped<PropertyValueTypeChangeGuard>();

		return services;
	}
}
