using Microsoft.Extensions.Hosting;
using RoyalIdentity.Configuration;

namespace RoyalIdentity.Storage.EntityFramework.Operational.Protection;

/// <summary>
/// Fails startup when an enabled realm selects an Operational payload profile that the composition did not
/// register. It must be registered after the Configuration snapshot hosted service so the initial snapshot is
/// already available when this validator starts.
/// </summary>
public sealed class OperationalPayloadProfilesStartupValidator(
    IConfigurationSnapshot snapshot,
    OperationalPayloadProtectorResolver profiles) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (!snapshot.IsLoaded)
        {
            throw new InvalidOperationException(
                "The Configuration snapshot must be loaded before Operational payload profiles are validated.");
        }

        foreach (var realmPath in snapshot.RealmPaths)
        {
            var realm = snapshot.FindRealmByPath(realmPath);
            if (realm is null || !realm.Enabled)
                continue;

            var profileId = realm.Options.OperationalStorage.PayloadProtectionProfile;
            try
            {
                _ = profiles.GetForWrite(profileId);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                throw new InvalidOperationException(
                    $"Enabled realm '{realm.Id}' selects unavailable Operational payload profile '{profileId}'.",
                    exception);
            }
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
