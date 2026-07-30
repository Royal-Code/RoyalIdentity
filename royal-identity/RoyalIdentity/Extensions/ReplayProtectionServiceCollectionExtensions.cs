using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using RoyalIdentity.Contracts.Defaults.ReplayProtection;
using RoyalIdentity.Contracts.Storage;

namespace RoyalIdentity.Extensions;

/// <summary>
/// Replay-protection selection. The composition root chooses the backing by calling one of these extensions;
/// there is no configuration key that picks an implementation, and no default. Configuration supplies
/// parameters, never the choice.
/// </summary>
public static class ReplayProtectionServiceCollectionExtensions
{
    private const string InMemoryStrategyName = "in-memory";

    /// <summary>
    /// Declares replay protection held in this process only.
    /// <para>
    ///     Valid for a single instance: a handle replayed against another replica is not seen. Replicated
    ///     deployments must declare a shared store instead. The store warns on construction, so the limitation
    ///     also reaches the logs of whoever runs it.
    /// </para>
    /// </summary>
    public static IServiceCollection AddInMemoryReplayProtection(this IServiceCollection services)
        => services.AddInMemoryReplayProtection(InMemoryReplayProtectionStore.DefaultPruneInterval);

    /// <summary>
    /// Declares replay protection held in this process only, pruning expired records at
    /// <paramref name="pruneInterval"/>. See <see cref="AddInMemoryReplayProtection(IServiceCollection)"/> for
    /// the limitation this choice accepts.
    /// </summary>
    public static IServiceCollection AddInMemoryReplayProtection(
        this IServiceCollection services, TimeSpan pruneInterval)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(pruneInterval, TimeSpan.Zero);

        // Hosts already provide one, but a bare ServiceCollection does not, and a factory registration is not
        // covered by ValidateOnBuild.
        services.TryAddSingleton(TimeProvider.System);

        services.AddSingleton<IReplayProtectionStore>(provider => new InMemoryReplayProtectionStore(
            provider.GetRequiredService<TimeProvider>(),
            provider.GetRequiredService<ILogger<InMemoryReplayProtectionStore>>(),
            pruneInterval));

        services.AddSingleton(new ReplayProtectionRegistration(
            InMemoryStrategyName,
            $"{nameof(AddInMemoryReplayProtection)}()",
            typeof(InMemoryReplayProtectionStore)));

        return services;
    }
}
