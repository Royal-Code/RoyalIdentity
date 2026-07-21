using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RoyalCode.SmartCommands.WorkContext.Extensions;
using RoyalCode.SmartCommands.WorkContext.Options;
using RoyalCode.WorkContext;
using RoyalCode.WorkContext.EntityFramework.Configurations;
using RoyalIdentity.UserAccounts.Features.Accounts.Commons;

namespace RoyalIdentity.UserAccounts.Infrastructure.Data;

/// <summary>
/// Registers the UserAccounts module persistence (repositories, searches, queries) and use-case handlers
/// on a WorkContext builder. Provider projects call this after wiring their EF Core provider connection.
/// </summary>
public static class UserAccountsWorkContextExtensions
{
	/// <summary>
	/// Configures persistence and the minimal IdP-integration use cases for the UserAccounts module.
	/// </summary>
	/// <typeparam name="TDbContext">The module DbContext type (the provider subclass).</typeparam>
	/// <param name="builder">The WorkContext builder.</param>
	/// <returns>The same builder for chaining.</returns>
	public static IWorkContextBuilder<TDbContext> ConfigureUserAccounts<TDbContext>(
		this IWorkContextBuilder<TDbContext> builder)
		where TDbContext : UserAccountsDbContext
	{
		var moduleAssembly = typeof(UserAccountsAssemblyMarker).Assembly;

		builder.ConfigureRepositories(moduleAssembly);
		builder.ConfigureSearches(moduleAssembly);

		// The module's commands and queries target the provider-agnostic base context; alias it to the
		// concrete provider context resolved by the WorkContext so both share the same scoped instance.
		builder.Services.AddScoped<UserAccountsDbContext>(sp => sp.GetRequiredService<TDbContext>());

		// The retry options below are bound from configuration (AddUnitOfWorkAccessor -> BindConfiguration), which
		// requires an IConfiguration in the container. Real hosts always register one; this fallback keeps the
		// module self-sufficient in bare-ServiceCollection scenarios (unit tests) that don't build a host.
		builder.Services.TryAddSingleton<IConfiguration>(new ConfigurationBuilder().Build());

		// SmartCommands unit-of-work adapter over IWorkContext, used by the generated command handlers.
		builder.Services.ConfigureWorkContextAdapterOptions(_ => { });
		builder.Services.AddUnitOfWorkAccessor<IWorkContext>();

		// Fixed module invariant (ADR-017 §2.9 / plan-users-accounts-sqlite-hardening Fase 1): every
		// optimistic-concurrency retry exhaustion in this module maps to this problem typeId. MaxAttempts stays
		// appsettings-configurable via the "RetryOnConcurrency" section (bound by AddUnitOfWorkAccessor above);
		// this only fixes the typeId, which is not a deployment knob.
		builder.Services.Configure<RetryOnConcurrencyOptions>(
			o => o.ExhaustedProblemTypeId = "user_account.concurrency_conflict");

		// Generated DI registration for the module's command handlers (see UserAccountsCommandServices).
		builder.Services.AddUserAccountsHandlersServices<IWorkContext>();

		// Module use-case collaborators with a single, injectable home for each concern.
		builder.Services.AddUserAccountsServices();

		return builder;
	}
}
