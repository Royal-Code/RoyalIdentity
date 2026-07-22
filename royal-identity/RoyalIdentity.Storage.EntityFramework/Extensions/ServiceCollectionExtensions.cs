using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RoyalIdentity.Storage.EntityFramework.Configuration;

namespace RoyalIdentity.Storage.EntityFramework.Extensions;

public static class ServiceCollectionExtensions
{
	/// <summary>
	/// <para>
	///     Registers the scoped Configuration storage ports of the EF adapter over
	///     <typeparamref name="TContext"/> (plan DF3/DF6). The context itself is registered by the consumer
	///     (<c>AddDbContext</c> with the default context, or a custom combined context that applies the
	///     public mapping extension of exactly one provider).
	/// </para>
	/// <para>
	///     This registration NEVER provides <c>IStorage</c>, <c>IStorageProvider</c> or
	///     <c>IStorageSession</c> — there is no partial production gateway before Plano 3 (plan DF20) —
	///     and never applies migrations (plan DF11). The Configuration stores and the snapshot source are
	///     added over this same seam by the following phases.
	/// </para>
	/// </summary>
	public static IServiceCollection AddEntityFrameworkConfigurationStorage<TContext>(this IServiceCollection services)
		where TContext : DbContext
	{
		ArgumentNullException.ThrowIfNull(services);

		services.TryAddScoped<IConfigurationDbContextAccessor, ConfigurationDbContextAccessor<TContext>>();

		return services;
	}
}
