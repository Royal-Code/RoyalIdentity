using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using RoyalIdentity.Contracts.Storage;

namespace RoyalIdentity.Contracts.Defaults;

/// <summary>
/// Fails host startup when an enabled realm cannot materialize a current signing credential for its main
/// configured algorithm. This is deliberately separate from <see cref="IServerJob"/>: server jobs isolate
/// their failures, while this invariant must prevent the server from accepting traffic.
/// </summary>
public sealed class SigningKeyStartupValidator(IServiceProvider applicationServices) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = applicationServices.CreateAsyncScope();
        var storage = scope.ServiceProvider.GetRequiredService<IStorage>();
        var keyManager = scope.ServiceProvider.GetRequiredService<IKeyManager>();

        // Materialize before asking the key store. EF providers may use the same scoped DbContext for both
        // stores; querying keys while the realm reader is still open causes overlapping commands on Npgsql.
        List<RoyalIdentity.Models.Realm> realms = [];
        await foreach (var realm in storage.Realms.GetAllAsync(cancellationToken))
            realms.Add(realm);

        foreach (var realm in realms)
        {
            if (!realm.Enabled)
                continue;

            SigningCredentials? credentials;
            try
            {
                credentials = await keyManager.GetSigningCredentialsAsync(realm, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw UnusableKey(realm.Id, exception);
            }

            if (credentials is null)
                throw UnusableKey(realm.Id);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private static InvalidOperationException UnusableKey(string realmId, Exception? inner = null)
        => new(
            $"Enabled realm '{realmId}' has no usable current signing key for its configured main algorithm.",
            inner);
}
