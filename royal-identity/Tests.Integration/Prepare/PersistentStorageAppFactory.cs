using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using RoyalIdentity.Configuration;
using RoyalIdentity.Data.Configuration;
using RoyalIdentity.Data.Operational;
using RoyalIdentity.Contracts.Storage;
using RoyalIdentity.Extensions;
using RoyalIdentity.Migrations;
using RoyalIdentity.Options;
using RoyalIdentity.Storage.EntityFramework.Configuration.Resources;
using RoyalIdentity.Storage.EntityFramework.Extensions;
using RoyalIdentity.Storage.EntityFramework.Operational.Maintenance;
using RoyalIdentity.Storage.EntityFramework.Sqlite;
using RoyalIdentity.UserAccounts.Infrastructure.Data;
using RoyalIdentity.UserAccounts.Integration;
using RoyalIdentity.UserAccounts.Options;
using RoyalIdentity.UserAccounts.Sqlite;
using Tests.UserAccounts;

namespace Tests.Integration.Prepare;

/// <summary>
/// Canonical persistent test composition introduced by Plan 4 Fase 4: Configuration and Operational share one
/// named SQLite in-memory database, while UserAccounts owns another. Every database is migrated by the runner
/// before the host starts; the host itself remains migration-free.
/// </summary>
public class PersistentStorageAppFactory : AppFactoryBase
{
    private readonly PersistentStorageLifetime lifetime = new();
    private readonly TestConfigurationResourceSource resourceSource = new();
    private bool provisioned;
    private bool disposed;

    public PersistentStorageAppFactory()
    {
        Handles = new PersistentStorageHandles();
        resourceSource.SetResourceServer(
            Handles.Demo.Id,
            TestConfigurationResourceSource.CreateDemoResourceServer());
    }

    public PersistentStorageHandles Handles { get; }

    public TestConfigurationResourceSource Resources => resourceSource;

    public PersistentStorageScope CreateStorageScope()
        => new(Services.CreateScope());

    internal string IdpConnectionString => lifetime.IdpConnectionString;

    internal string UserAccountsConnectionString => lifetime.UserAccountsConnectionString;

    internal string DataProtectionKeyRingPath => KeyRingPath;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        Provision();

        builder.UseDefaultServiceProvider(options =>
        {
            options.ValidateOnBuild = true;
            options.ValidateScopes = true;
        });

        builder.ConfigureServices(services =>
        {
            RegisterIdpStorage(services);

            services.AddEntityFrameworkConfigurationSnapshotSource();
            services.AddSingleton(new ConfigurationSnapshotRefreshOptions
            {
                RefreshInterval = TimeSpan.FromMinutes(5),
            });
            services.Replace(ServiceDescriptor.Singleton<IConfigurationResourceSource>(resourceSource));

            services.AddOperationalDataProtectionPayloadProtection(
                OperationalStorageOptions.DefaultPayloadProtectionProfile);
            services.AddEntityFrameworkOperationalCleanup(
                options => options.Mode = CleanupExecutionMode.External);
            services.AddAspNetDataProtectionKeyMaterialProtector();
            services.AddEntityFrameworkStorage();

            RegisterReplayProtection(services);

            var accountOptions = CreateAccountOptions();
            services.AddSingleton<IUserAccountsRealmOptionsResolver>(
                new DefaultUserAccountsRealmOptionsResolver(accountOptions));
            services.AddUserAccountsSqliteConnection(lifetime.UserAccountsConnectionString);
            services.AddUserAccountsForRoyalIdentity();

            services.AddScoped<PersistentClientSetup>();
            services.AddScoped<PersistentAccountSetup>();
            services.AddScoped<PersistentOperationalSetup>();
            services.AddScoped<PersistentOperationalProbe>();
            InsertInitializerBeforeProtocolHostedServices(services);
            services.AddOperationalPayloadProfileStartupValidation();
        });
    }

    public async Task<RoyalIdentity.Models.Realm> LoadRealmAsync(
        TestRealmHandle handle,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(handle);
        using var scope = Services.CreateScope();
        var storage = scope.ServiceProvider.GetRequiredService<IStorage>();
        return await storage.Realms.GetByIdAsync(handle.Id, ct)
            ?? throw new InvalidOperationException($"Seeded realm '{handle.Id}' was not found.");
    }

    public async Task SaveClientAsync(
        TestRealmHandle realmHandle,
        string clientId,
        Action<TestClientBuilder> configure,
        CancellationToken ct = default)
    {
        var realm = await LoadRealmAsync(realmHandle, ct);
        using var scope = Services.CreateScope();
        await scope.ServiceProvider
            .GetRequiredService<PersistentClientSetup>()
            .SaveAsync(realm, clientId, configure, ct);
    }

    public async Task RefreshConfigurationAsync(CancellationToken ct = default)
    {
        using var scope = Services.CreateScope();
        await scope.ServiceProvider
            .GetRequiredService<IConfigurationSnapshotRefresher>()
            .RefreshAsync(ct);
    }

    public async Task<TResult> WithStorageAsync<TResult>(
        Func<IStorage, Task<TResult>> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        using var scope = Services.CreateScope();
        return await operation(scope.ServiceProvider.GetRequiredService<IStorage>());
    }

    public async Task<TResult> WithStorageValueAsync<TResult>(
        Func<IStorage, ValueTask<TResult>> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        using var scope = Services.CreateScope();
        return await operation(scope.ServiceProvider.GetRequiredService<IStorage>());
    }

    public async Task WithStorageAsync(Func<IStorage, Task> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        using var scope = Services.CreateScope();
        await operation(scope.ServiceProvider.GetRequiredService<IStorage>());
    }

    public async Task WithStorageValueAsync(Func<IStorage, ValueTask> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        using var scope = Services.CreateScope();
        await operation(scope.ServiceProvider.GetRequiredService<IStorage>());
    }

    public async Task UpdateRealmAsync(
        TestRealmHandle realmHandle,
        Action<RealmOptions> update,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(realmHandle);
        ArgumentNullException.ThrowIfNull(update);
        using var scope = Services.CreateScope();
        var storage = scope.ServiceProvider.GetRequiredService<IStorage>();
        var realm = await storage.Realms.GetByIdAsync(realmHandle.Id, ct)
            ?? throw new InvalidOperationException($"Realm '{realmHandle.Id}' was not found.");
        update(realm.Options);
        await storage.Realms.SaveAsync(realm, ct);
        await scope.ServiceProvider
            .GetRequiredService<IConfigurationSnapshotRefresher>()
            .RefreshAsync(ct);
    }

    public async Task SaveRealmAsync(
        RoyalIdentity.Models.Realm realm,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(realm);
        using var scope = Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IStorage>().Realms.SaveAsync(realm, ct);
        await scope.ServiceProvider
            .GetRequiredService<IConfigurationSnapshotRefresher>()
            .RefreshAsync(ct);
    }

    public async Task SetAccountActiveAsync(
        TestRealmHandle realm,
        TestSubjectHandle subject,
        bool active,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(realm);
        ArgumentNullException.ThrowIfNull(subject);
        using var scope = Services.CreateScope();
        await scope.ServiceProvider
            .GetRequiredService<PersistentAccountSetup>()
            .SetActiveAsync(realm.Id, subject.SubjectId, active, ct);
    }

    public async Task<PersistentAccountState?> FindAccountStateAsync(
        TestRealmHandle realm,
        TestSubjectHandle subject,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(realm);
        ArgumentNullException.ThrowIfNull(subject);
        using var scope = Services.CreateScope();
        return await scope.ServiceProvider
            .GetRequiredService<PersistentAccountSetup>()
            .FindStateAsync(realm.Id, subject.SubjectId, ct);
    }

    public async Task<IReadOnlyList<PersistentSessionState>> FindSessionsAsync(
        TestRealmHandle realm,
        TestSubjectHandle subject,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(realm);
        ArgumentNullException.ThrowIfNull(subject);
        using var scope = Services.CreateScope();
        return await scope.ServiceProvider
            .GetRequiredService<PersistentOperationalProbe>()
            .FindSessionsAsync(realm.Id, subject.SubjectId, ct);
    }

    public async Task SeedAccountAsync(
        TestRealmHandle realm,
        TestSubjectHandle subject,
        bool active = true,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(realm);
        ArgumentNullException.ThrowIfNull(subject);
        using var scope = Services.CreateScope();
        await scope.ServiceProvider
            .GetRequiredService<PersistentAccountSetup>()
            .SeedAsync(realm.Id, subject, active, ct);
    }

    public async Task SetAccountClaimAsync(
        TestRealmHandle realm,
        TestSubjectHandle subject,
        string scopeName,
        string claimType,
        IReadOnlyList<string> values,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(realm);
        ArgumentNullException.ThrowIfNull(subject);
        using var scope = Services.CreateScope();
        await scope.ServiceProvider
            .GetRequiredService<PersistentAccountSetup>()
            .SetClaimAsync(realm.Id, subject.SubjectId, scopeName, claimType, values, ct);
    }

    public async Task EnsureAccountClaimDefinitionAsync(
        TestRealmHandle realm,
        string scopeName,
        string claimType,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(realm);
        using var scope = Services.CreateScope();
        await scope.ServiceProvider
            .GetRequiredService<PersistentAccountSetup>()
            .EnsureClaimDefinitionAsync(realm.Id, scopeName, claimType, ct);
    }

    public async Task SetRefreshTokenConsumedTimeAsync(
        TestRealmHandle realm,
        string refreshToken,
        DateTime consumedAtUtc,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(realm);
        using var scope = Services.CreateScope();
        await scope.ServiceProvider
            .GetRequiredService<PersistentOperationalSetup>()
            .SetRefreshTokenConsumedTimeAsync(realm.Id, refreshToken, consumedAtUtc, ct);
    }

    /// <summary>
    /// Registers the IdP storage graph — the two contexts and the EF families over them. A provider variant
    /// overrides this together with <see cref="ProvisionIdpStorage"/>; everything else in this composition is
    /// provider-neutral.
    /// </summary>
    protected virtual void RegisterIdpStorage(IServiceCollection services)
    {
        services.AddDbContext<ConfigurationSqliteDbContext>(options => options.UseSqlite(
            lifetime.IdpConnectionString,
            sqlite => sqlite.UseConfigurationMigrationsHistory()));
        services.AddDbContext<OperationalSqliteDbContext>(options => options.UseSqlite(
            lifetime.IdpConnectionString,
            sqlite => sqlite.UseOperationalMigrationsHistory()));

        services.AddEntityFrameworkConfigurationStorage<ConfigurationSqliteDbContext>();
        services.AddEntityFrameworkOperationalStorage<OperationalSqliteDbContext>();

        RegisterContextAliases<ConfigurationSqliteDbContext, OperationalSqliteDbContext>(services);
    }

    /// <summary>
    /// The test-only write and probe seams are provider-neutral and reach each family through these aliases, so
    /// a provider variant does not need seams of its own.
    /// </summary>
    protected static void RegisterContextAliases<TConfiguration, TOperational>(IServiceCollection services)
        where TConfiguration : ConfigurationDbContext
        where TOperational : OperationalDbContext
    {
        services.AddScoped<ConfigurationDbContext>(
            provider => provider.GetRequiredService<TConfiguration>());
        services.AddScoped<OperationalDbContext>(
            provider => provider.GetRequiredService<TOperational>());
    }

    /// <summary>
    /// Declares the replay-protection backing (plan-replay-protection DF12): the fixture declares it like any
    /// other composition root. A provider variant overrides this to exercise the durable backing instead.
    /// </summary>
    protected virtual void RegisterReplayProtection(IServiceCollection services)
        => services.AddInMemoryReplayProtection();

    /// <summary>
    /// Applies Configuration and Operational plus the product seed. A provider variant overrides it; the
    /// UserAccounts family is applied by the caller and stays on SQLite either way, because no scenario of this
    /// fixture makes the account backing its subject.
    /// </summary>
    protected virtual void ProvisionIdpStorage()
        => EnsureSucceeded(StorageMigrationRunner.RunAsync(new MigrationRunnerOptions
        {
            ConfigurationProvider = ConfigurationDatabaseProvider.Sqlite,
            Families = StorageFamilySelection.Configuration | StorageFamilySelection.Operational,
            ConfigurationConnection = lifetime.IdpConnectionString,
            OperationalConnection = lifetime.IdpConnectionString,
            DatabaseTopology = StorageDatabaseTopology.Shared,
            Seed = ConfigurationSeedMode.All,
            ProductSeed = ProductSeedOptions,
            KeyProtector = ConfigurationKeyProtector.DataProtection,
            DataProtectionKeyRing = KeyRingPath,
            DataProtectionApplicationName = DataProtectionApplicationName,
        }).GetAwaiter().GetResult());

    /// <summary>The product seed every variant applies, so the seeded realms and client stay identical.</summary>
    protected static ConfigurationProductSeedOptions ProductSeedOptions => new()
    {
        ServerAdminRedirectUris = ["https://localhost/server-admin/callback"],
    };

    private void Provision()
    {
        if (provisioned)
            return;

        provisioned = true;
        lifetime.OpenAsync().GetAwaiter().GetResult();

        try
        {
            ProvisionIdpStorage();

            EnsureSucceeded(StorageMigrationRunner.RunAsync(new MigrationRunnerOptions
            {
                ConfigurationProvider = ConfigurationDatabaseProvider.Sqlite,
                Families = StorageFamilySelection.UserAccounts,
                UserAccountsConnection = lifetime.UserAccountsConnectionString,
            }).GetAwaiter().GetResult());
        }
        catch
        {
            lifetime.Dispose();
            throw;
        }
    }

    private static void InsertInitializerBeforeProtocolHostedServices(IServiceCollection services)
    {
        var descriptor = ServiceDescriptor.Singleton<IHostedService, PersistentStorageInitializer>();
        var firstHostedService = services
            .Select((service, index) => (service, index))
            .Where(item => item.service.ServiceType == typeof(IHostedService))
            .Select(item => item.index)
            .DefaultIfEmpty(services.Count)
            .First();
        services.Insert(firstHostedService, descriptor);
    }

    private static UserAccountsRealmOptions CreateAccountOptions()
    {
        var options = new UserAccountsRealmOptions
        {
            AllowProvidedSubjectId = true,
        };
        options.PasswordOptions.MinimumLength = 1;
        options.PasswordOptions.RequireSpecialCharacters = false;
        options.PasswordOptions.RequireDigit = false;
        options.PasswordOptions.RequireUppercase = false;
        options.PasswordOptions.RequireLowercase = false;
        options.PasswordOptions.MinimumUniqueCharacters = 0;
        options.PasswordOptions.DisallowUsernameInPassword = false;
        return options;
    }

    protected static void EnsureSucceeded(StorageMigrationReport report)
    {
        var failure = report.Families.FirstOrDefault(result =>
            result.Status is StorageMigrationStatus.Failed);
        if (failure is not null)
        {
            throw new InvalidOperationException(
                $"Persistent test provisioning failed for storage family '{failure.Family}'.",
                failure.Failure);
        }
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (!disposing || disposed)
            return;

        disposed = true;
        lifetime.Dispose();
    }

    private sealed class PersistentStorageInitializer(
        IServiceScopeFactory scopeFactory,
        IUserAccountsRealmOptionsResolver optionsResolver) : IHostedService
    {
        public async Task StartAsync(CancellationToken ct)
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<UserAccountsDbContext>();
            var now = DateTimeOffset.UtcNow;

            foreach (var realmId in RealmIds)
            {
                await UserAccountsModuleSeed.SeedDefaultScopesAsync(db, realmId, now, ct);
                await UserAccountsModuleSeed.SeedDefaultAccountsAsync(
                    scope.ServiceProvider,
                    realmId,
                    optionsResolver.Resolve(realmId),
                    now,
                    ct);
            }
        }

        public Task StopAsync(CancellationToken ct) => Task.CompletedTask;

        private static readonly string[] RealmIds =
        [
            Constants.Server.Realms.ServerRealm,
            Constants.Server.Realms.AccountRealm,
            Constants.Server.Realms.AdminRealm,
            "demo_realm",
        ];
    }
}

public sealed class PersistentStorageScope(IServiceScope scope) : IDisposable
{
    public IStorage Storage { get; } = scope.ServiceProvider.GetRequiredService<IStorage>();

    public void Dispose() => scope.Dispose();
}
