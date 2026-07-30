using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RoyalIdentity.Contracts.Defaults.ReplayProtection;
using RoyalIdentity.Contracts.Storage;

namespace Tests.Integration.Prepare;

/// <summary>
/// <see cref="PersistentStorageAppFactory"/> whose replay-protection backing always fails, standing in for a
/// database that is down. It proves the failure is not translated into an authentication outcome — neither an
/// accepted credential nor an <c>invalid_client</c> indistinguishable from a genuine credential failure.
/// <para>
/// It declares its own strategy marker, exactly as a third-party backing would: replacing only the store would be
/// refused at startup as an inconsistent declaration.
/// </para>
/// </summary>
public class FailingReplayProtectionAppFactory : PersistentStorageAppFactory
{
    /// <summary>Message the failing backing throws with, so a test can identify it.</summary>
    public const string FailureMessage = "replay protection backing is unavailable";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.ConfigureServices(services =>
        {
            // Both the store and the marker are replaced: leaving the in-memory declaration in place would be
            // two declared strategies, which the startup validator refuses — as it should.
            services.RemoveAll<IReplayProtectionStore>();
            services.RemoveAll<ReplayProtectionRegistration>();

            services.AddSingleton<IReplayProtectionStore, FailingReplayProtectionStore>();
            services.AddSingleton(new ReplayProtectionRegistration(
                "failing", "AddFailingReplayProtection()", typeof(FailingReplayProtectionStore)));
        });
    }

    private sealed class FailingReplayProtectionStore : IReplayProtectionStore
    {
        public Task<bool> TryAddAsync(
            string realmId,
            string issuer,
            string purpose,
            string handle,
            DateTimeOffset expiration,
            CancellationToken ct) => throw new InvalidOperationException(FailureMessage);
    }
}
