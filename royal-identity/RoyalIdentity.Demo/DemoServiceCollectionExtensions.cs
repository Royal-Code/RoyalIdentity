using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using RoyalIdentity.Configuration;
using RoyalIdentity.Extensions;
using RoyalIdentity.Options;
using RoyalIdentity.Storage.EntityFramework.Extensions;
using RoyalIdentity.Storage.EntityFramework.Operational.Maintenance;
using RoyalIdentity.Storage.EntityFramework.Sqlite;

namespace RoyalIdentity.Demo;

public static class DemoServiceCollectionExtensions
{
    public static IServiceCollection AddRoyalIdentityDemo(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        var lifetime = new DemoStorageLifetime();
        services.AddSingleton(_ => lifetime);

        services.AddDataProtection()
            .SetApplicationName(DemoConstants.DataProtectionApplicationName)
            .PersistKeysToFileSystem(new DirectoryInfo(lifetime.KeyRingPath));

        services.AddDbContext<ConfigurationSqliteDbContext>(options => options.UseSqlite(
            lifetime.IdpConnectionString,
            sqlite => sqlite.UseConfigurationMigrationsHistory()));
        services.AddDbContext<OperationalSqliteDbContext>(options => options.UseSqlite(
            lifetime.IdpConnectionString,
            sqlite => sqlite.UseOperationalMigrationsHistory()));

        services.AddEntityFrameworkConfigurationStorage<ConfigurationSqliteDbContext>();
        services.AddEntityFrameworkConfigurationSnapshotSource();
        services.AddSingleton(new ConfigurationSnapshotRefreshOptions
        {
            RefreshInterval = TimeSpan.FromMinutes(5),
        });
        services.AddEntityFrameworkConfigurationDemoResources(DemoConstants.RealmId);

        services.AddEntityFrameworkOperationalStorage<OperationalSqliteDbContext>();
        services.AddOperationalDataProtectionPayloadProtection(
            OperationalStorageOptions.DefaultPayloadProtectionProfile);
        services.AddEntityFrameworkOperationalCleanup(
            options => options.Mode = CleanupExecutionMode.External);
        services.AddAspNetDataProtectionKeyMaterialProtector();
        services.AddEntityFrameworkStorage();

        // Coherent with what the Demo is: a single ephemeral process whose whole database dies with it.
        services.AddInMemoryReplayProtection();

        services.AddUserAccountsSqliteConnection(lifetime.UserAccountsConnectionString);
        services.AddUserAccountsForRoyalIdentity();

        // Must precede AddOpenIdConnectProviderServices: hosted services start in registration order, so the
        // database exists before the snapshot, signing-key and payload-profile validators inspect it.
        services.AddHostedService<DemoStorageInitializer>();
        services.AddRoyalIdentityRazor();
        services.AddOpenIdConnectProviderServices();
        services.AddOperationalPayloadProfileStartupValidation();

        return services;
    }
}
