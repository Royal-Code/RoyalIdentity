using RoyalIdentity.Migrations;
using RoyalIdentity.UserAccounts.Features.Accounts.UseCases;
using RoyalIdentity.UserAccounts.Integration;

namespace RoyalIdentity.Demo;

/// <summary>
/// Provisions the empty, process-local databases before the snapshot and protocol validators start.
/// </summary>
public sealed class DemoStorageInitializer(
    DemoStorageLifetime lifetime,
    IServiceProvider services) : IHostedService
{
    public async Task StartAsync(CancellationToken ct)
    {
        await lifetime.OpenAsync(ct);

        var idpReport = await StorageMigrationRunner.RunAsync(new MigrationRunnerOptions
        {
            ConfigurationProvider = ConfigurationDatabaseProvider.Sqlite,
            Families = StorageFamilySelection.Configuration | StorageFamilySelection.Operational,
            ConfigurationConnection = lifetime.IdpConnectionString,
            OperationalConnection = lifetime.IdpConnectionString,
            DatabaseTopology = StorageDatabaseTopology.Shared,
            Seed = ConfigurationSeedMode.Demo,
            KeyProtector = ConfigurationKeyProtector.DataProtection,
            DataProtectionKeyRing = lifetime.KeyRingPath,
            DataProtectionApplicationName = DemoConstants.DataProtectionApplicationName,
        }, ct);
        EnsureSucceeded(idpReport);

        var accountsReport = await StorageMigrationRunner.RunAsync(new MigrationRunnerOptions
        {
            ConfigurationProvider = ConfigurationDatabaseProvider.Sqlite,
            Families = StorageFamilySelection.UserAccounts,
            UserAccountsConnection = lifetime.UserAccountsConnectionString,
        }, ct);
        EnsureSucceeded(accountsReport);

        await using var scope = services.CreateAsyncScope();
        var options = scope.ServiceProvider
            .GetRequiredService<IUserAccountsRealmOptionsResolver>()
            .Resolve(DemoConstants.RealmId);
        var result = await scope.ServiceProvider
            .GetRequiredService<ICreateUserAccountHandler>()
            .HandleAsync(new CreateUserAccount
            {
                RealmId = DemoConstants.RealmId,
                Options = options,
                Username = DemoConstants.Username,
                DisplayName = "Demo User",
                Email = "alice@demo.com",
                EmailVerified = true,
                Password = DemoConstants.Password,
                Roles = ["admin"],
            }, ct);

        if (!result.HasValue(out _))
            throw new InvalidOperationException("The Demo account could not be provisioned.");
    }

    public Task StopAsync(CancellationToken ct)
    {
        lifetime.Dispose();
        return Task.CompletedTask;
    }

    private static void EnsureSucceeded(StorageMigrationReport report)
    {
        var failure = report.Families.FirstOrDefault(result =>
            result.Status is StorageMigrationStatus.Failed);
        if (failure is not null)
        {
            throw new InvalidOperationException(
                $"Demo provisioning failed for storage family '{failure.Family}'.",
                failure.Failure);
        }
    }
}
