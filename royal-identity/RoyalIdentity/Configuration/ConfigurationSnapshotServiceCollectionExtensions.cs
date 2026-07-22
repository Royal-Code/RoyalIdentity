using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace RoyalIdentity.Configuration;

/// <summary>
/// Registers the configuration snapshot infrastructure (plan DF7): the singleton holder that is the synchronous
/// view, the refresher that loads/publishes/invalidates, and the hosted service that bootstraps before traffic
/// and refreshes periodically. The snapshot <b>source</b> and the mandatory
/// <see cref="ConfigurationSnapshotRefreshOptions"/> are supplied by the storage backing, not here.
/// </summary>
public static class ConfigurationSnapshotServiceCollectionExtensions
{
	public static IServiceCollection AddConfigurationSnapshot(this IServiceCollection services)
	{
		ArgumentNullException.ThrowIfNull(services);

		services.TryAddSingleton(TimeProvider.System);
		services.AddSingleton<ConfigurationSnapshotHolder>();
		services.TryAddSingleton<IConfigurationSnapshot>(sp => sp.GetRequiredService<ConfigurationSnapshotHolder>());
		services.TryAddSingleton<IConfigurationSnapshotRefresher, ConfigurationSnapshotRefresher>();
		services.AddHostedService<ConfigurationSnapshotHostedService>();

		return services;
	}
}
