using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RoyalIdentity.Contracts.Storage;

namespace RoyalIdentity.Contracts.Defaults.ReplayProtection;

/// <summary>
/// <para>
///     Fails host startup when the composition did not declare exactly one replay-protection strategy. There is
///     no default: a default that silently resolves is what let a no-op implementation pass for protection.
/// </para>
/// <para>
///     This runs in every environment on purpose. <c>WebApplication.CreateBuilder</c> only turns on
///     <c>ValidateOnBuild</c> in Development, so in Production a missing registration would otherwise surface as
///     an <c>invalid_client</c> on the first <c>private_key_jwt</c> authentication — indistinguishable from a
///     genuine credential failure.
/// </para>
/// </summary>
public sealed class ReplayProtectionStartupValidator(IServiceProvider applicationServices) : IHostedService
{
    /// <summary>Names the extensions an operator can call, quoted in every failure message.</summary>
    internal const string AvailableStrategies =
        "Call AddInMemoryReplayProtection() (single instance only) or AddOperationalReplayProtection() " +
        "(durable and shared across instances) exactly once.";

    public Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = applicationServices.CreateScope();
        var registrations = scope.ServiceProvider.GetServices<ReplayProtectionRegistration>().ToList();
        var store = scope.ServiceProvider.GetService<IReplayProtectionStore>();

        if (registrations.Count is 0)
        {
            throw store is null
                ? Refuse("no replay-protection strategy was declared")
                : Refuse(
                    $"a replay-protection store ('{store.GetType().FullName}') is registered without a " +
                    "strategy declaration, so the composition's intent cannot be verified");
        }

        if (registrations.Count > 1)
        {
            var declared = string.Join(", ", registrations.Select(item => item.ExtensionName));
            throw Refuse(
                $"more than one replay-protection strategy was declared ({declared}); registration order would " +
                "silently decide which one wins");
        }

        var registration = registrations[0];

        if (store is null)
        {
            throw Refuse(
                $"'{registration.ExtensionName}' declared the '{registration.StrategyName}' strategy but no " +
                $"{nameof(IReplayProtectionStore)} is registered");
        }

        if (!registration.StoreType.IsInstanceOfType(store))
        {
            throw Refuse(
                $"'{registration.ExtensionName}' declared the '{registration.StrategyName}' strategy backed by " +
                $"'{registration.StoreType.FullName}', but the container resolves '{store.GetType().FullName}'");
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private static InvalidOperationException Refuse(string reason)
        => new($"Replay protection is not usable: {reason}. {AvailableStrategies}");
}
