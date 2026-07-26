using Microsoft.AspNetCore.Hosting;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RoyalIdentity.Configuration;
using RoyalIdentity.Contracts.Storage;
using RoyalIdentity.Migrations;
using RoyalIdentity.Options;
using RoyalIdentity.Storage.EntityFramework.Extensions;
using RoyalIdentity.Storage.EntityFramework.Operational.Maintenance;
using RoyalIdentity.Storage.EntityFramework.Sqlite;
using RoyalIdentity.Storage.InMemory;

namespace Tests.Integration.Prepare;

/// <summary>
/// Test host variant that swaps the IdP storage for the <b>complete EF gateway</b> — Configuration and
/// Operational, both families over one SQLite database created by the checked-in migrations and seeded by the
/// production runner (plan Fase 8). The account edge stays in memory, because who holds the users is a
/// different seam from where protocol state is persisted (ADR-014/015).
/// <para>
/// It is opt-in and changes no default: <see cref="AppFactory"/> and every suite built on it keep the in-memory
/// backing (ADR-018). This factory exists so at least one full OIDC flow is proven end to end over the backing
/// Plano 4 will adopt.
/// </para>
/// </summary>
public sealed class EntityFrameworkStorageAppFactory : AppFactory
{
    private readonly string databasePath =
        Path.Combine(Path.GetTempPath(), $"royalidentity-oidc-ef-{Guid.NewGuid():N}.db");
    private SqliteConnection? keepAlive;

    private string ConnectionString => $"Data Source={databasePath}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        // The runner is the only thing that applies migrations, exactly as in production: the host never does
        // (plan DF23). Both families go into one database, which is the topology the two histories exist for.
        MigrateAndSeed();

        builder.ConfigureServices(services =>
        {
            RemoveInMemoryStorage(services);

            services.AddDbContext<ConfigurationSqliteDbContext>(options => options
                .UseSqlite(ConnectionString, sqlite => sqlite.UseConfigurationMigrationsHistory()));
            services.AddDbContext<OperationalSqliteDbContext>(options => options
                .UseSqlite(ConnectionString, sqlite => sqlite.UseOperationalMigrationsHistory()));

            services.AddEntityFrameworkConfigurationStorage<ConfigurationSqliteDbContext>();
            services.AddEntityFrameworkConfigurationSnapshotSource();
            services.AddSingleton(new ConfigurationSnapshotRefreshOptions
            {
                RefreshInterval = TimeSpan.FromMinutes(5),
            });
            // The demo realm needs its resource server through the volatile bridge; it is never implicit.
            services.AddEntityFrameworkConfigurationDemoResources(MemoryStorage.DemoRealm.Id);

            services.AddEntityFrameworkOperationalStorage<OperationalSqliteDbContext>();
            services.AddOperationalAesGcmPayloadProtection(
                OperationalStorageOptions.DefaultPayloadProtectionProfile, [.. ProtectorKey]);
            services.AddEntityFrameworkOperationalCleanup(
                cleanup => cleanup.Mode = CleanupExecutionMode.External);
            services.AddAesKeyMaterialProtector(options => options.Key = [.. ProtectorKey]);

            services.AddEntityFrameworkStorage();
        });
    }

    /// <summary>
    /// Applies both families and the demo seed through <see cref="StorageMigrationRunner"/>, then holds one
    /// connection open for the lifetime of the factory so the file is not reopened per request needlessly.
    /// </summary>
    private void MigrateAndSeed()
    {
        previousAesKey = Environment.GetEnvironmentVariable(AesKeyVariable);
        Environment.SetEnvironmentVariable(AesKeyVariable, Convert.ToBase64String([.. ProtectorKey]));

        var report = StorageMigrationRunner.RunAsync(new MigrationRunnerOptions
        {
            ConfigurationProvider = ConfigurationDatabaseProvider.Sqlite,
            ConfigurationConnection = ConnectionString,
            Families = StorageFamilySelection.All,
            Seed = ConfigurationSeedMode.Demo,
            KeyProtector = ConfigurationKeyProtector.Aes,
            AesKeyEnvironmentVariable = AesKeyVariable,
        }).GetAwaiter().GetResult();

        if (!report.Succeeded)
        {
            throw new InvalidOperationException(
                "The EF storage fixture could not migrate: " +
                string.Join(
                    "; ",
                    report.Families.Select(family => $"{family.Family}={family.Status}")));
        }

        keepAlive = new SqliteConnection(ConnectionString);
        keepAlive.Open();
    }

    /// <summary>
    /// The runner reads the signing-key protector key from an environment variable, so the fixture publishes it
    /// under a name of its own and restores whatever was there on disposal — a process-wide variable left behind
    /// is contamination between suites.
    /// </summary>
    private const string AesKeyVariable = "ROYALIDENTITY_EF_FIXTURE_AES_KEY";

    private string? previousAesKey;

    /// <summary>
    /// Drops the in-memory gateway registrations so the EF ones are the only <see cref="IStorage"/> in the
    /// composition. <c>MemoryStorage</c> itself stays: it still backs the account edge.
    /// </summary>
    private static void RemoveInMemoryStorage(IServiceCollection services)
    {
        services.RemoveAll<IStorage>();
        services.RemoveAll<IStorageProvider>();
        services.RemoveAll<IConfigurationSnapshotSource>();
        services.RemoveAll<ConfigurationSnapshotRefreshOptions>();
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (!disposing)
            return;

        keepAlive?.Dispose();
        keepAlive = null;
        SqliteConnection.ClearAllPools();
        Environment.SetEnvironmentVariable(AesKeyVariable, previousAesKey);

        try
        {
            if (File.Exists(databasePath))
                File.Delete(databasePath);
        }
        catch (IOException)
        {
            // A leftover temp file is not worth failing a green run over.
        }
    }

    private static ReadOnlySpan<byte> ProtectorKey
        =>
        [
            0x51, 0x52, 0x53, 0x54, 0x55, 0x56, 0x57, 0x58,
            0x59, 0x5A, 0x5B, 0x5C, 0x5D, 0x5E, 0x5F, 0x60,
            0x61, 0x62, 0x63, 0x64, 0x65, 0x66, 0x67, 0x68,
            0x69, 0x6A, 0x6B, 0x6C, 0x6D, 0x6E, 0x6F, 0x70,
        ];
}
