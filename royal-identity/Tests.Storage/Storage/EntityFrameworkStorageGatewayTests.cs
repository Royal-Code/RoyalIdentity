using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using RoyalIdentity.Configuration;
using RoyalIdentity.Contracts.Storage;
using RoyalIdentity.Data.Configuration.Entities;
using RoyalIdentity.Models;
using RoyalIdentity.Models.Tokens;
using RoyalIdentity.Options;
using RoyalIdentity.Storage.EntityFramework.Extensions;
using RoyalIdentity.Storage.EntityFramework.Operational.Maintenance;
using RoyalIdentity.Storage.EntityFramework.Operational.Stores;
using RoyalIdentity.Storage.EntityFramework.Sqlite;
using Tests.Storage.Operational.Support;
using Tests.Storage.Support;

namespace Tests.Storage;

/// <summary>
/// The complete EF gateway (plan Fase 6, DF21/DF22): <c>IStorage</c> composing both families, and
/// <c>IStorageProvider</c>/<c>IStorageSession</c> as a <b>lifetime</b> seam — a DI scope, never a unit of work,
/// because Configuration and Operational may live in different databases and no transaction can span them.
/// </summary>
public class EntityFrameworkStorageGatewayTests
{
    private static readonly DateTime Start = StorageContractHarness.Start;

    // Every member of IStorage resolves through the gateway, both families included.
    [Fact]
    public async Task Storage_ResolvesEveryMember_FromBothFamilies()
    {
        await using var composition = await GatewayComposition.CreateAsync();
        using var scope = composition.Services.CreateScope();
        var storage = scope.ServiceProvider.GetRequiredService<IStorage>();
        var realm = await composition.LoadRealmAsync(storage);

        Assert.NotNull(storage.ServerOptions);
        Assert.NotNull(storage.Realms);
        Assert.NotNull(storage.GetClientStore(realm));
        Assert.NotNull(storage.GetKeyStore(realm));
        Assert.NotNull(storage.GetResourceStore(realm));
        Assert.NotNull(storage.GetAccessTokenStore(realm));
        Assert.NotNull(storage.GetRefreshTokenStore(realm));
        Assert.NotNull(storage.GetAuthorizationCodeStore(realm));
        Assert.NotNull(storage.GetUserConsentStore(realm));
        Assert.NotNull(storage.GetUserSessionStore(realm));
        Assert.NotNull(storage.GetAuthorizeParametersStore(realm));
    }

    // DF46: the base Operational contracts require the atomic operations at compile time.
    [Fact]
    public async Task Storage_ExposesTheDefinitiveAtomicOperationalContracts()
    {
        await using var composition = await GatewayComposition.CreateAsync();
        using var scope = composition.Services.CreateScope();
        var storage = scope.ServiceProvider.GetRequiredService<IStorage>();
        var realm = await composition.LoadRealmAsync(storage);

        Assert.IsAssignableFrom<IAuthorizationCodeStore>(storage.GetAuthorizationCodeStore(realm));
        Assert.IsAssignableFrom<IRefreshTokenStore>(storage.GetRefreshTokenStore(realm));
    }

    // DF22: the synchronous member reads the published snapshot, so it opens no connection and issues no command.
    [Fact]
    public async Task ServerOptions_ComesFromTheSnapshot_WithoutTouchingTheDatabase()
    {
        await using var composition = await GatewayComposition.CreateAsync();
        using var scope = composition.Services.CreateScope();
        var storage = scope.ServiceProvider.GetRequiredService<IStorage>();
        composition.Commands.Reset();

        var first = storage.ServerOptions;
        var second = storage.ServerOptions;

        Assert.Equal(0, composition.Commands.Count);
        // The snapshot hands out defensive copies, so a caller mutating one cannot poison the next read.
        Assert.NotSame(first, second);

        // The counter is not vacuously zero: a real read through the same gateway does reach the database.
        await storage.Realms.GetByIdAsync("gateway-realm", default);
        Assert.True(composition.Commands.Count > 0, "the command counter never observed a database round-trip");
    }

    // DF3: each session is its own scope, hence its own storage — never a shared one.
    [Fact]
    public async Task EachSession_GetsItsOwnStorage()
    {
        await using var composition = await GatewayComposition.CreateAsync();
        var provider = composition.Services.GetRequiredService<IStorageProvider>();

        using var first = provider.CreateSession();
        using var second = provider.CreateSession();

        Assert.NotSame(first, second);
        Assert.NotSame(first.GetStorage(), second.GetStorage());
        // Inside one session the storage is the scoped instance, stable across resolutions.
        Assert.Same(first.GetStorage(), first.GetStorage());
    }

    // DF2: the two families keep their own context — and their own change tracker — even over one database, and
    // a scope is exactly what a session is.
    [Fact]
    public async Task EachScope_KeepsOneContextPerFamily_AndNoneIsSharedAcrossScopes()
    {
        await using var composition = await GatewayComposition.CreateAsync();

        using var first = composition.Services.CreateScope();
        using var second = composition.Services.CreateScope();

        var configuration = first.ServiceProvider.GetRequiredService<ConfigurationSqliteDbContext>();
        var operational = first.ServiceProvider.GetRequiredService<OperationalSqliteDbContext>();

        Assert.NotSame((DbContext)configuration, operational);
        Assert.NotSame(configuration.ChangeTracker, operational.ChangeTracker);
        Assert.NotSame(configuration, second.ServiceProvider.GetRequiredService<ConfigurationSqliteDbContext>());
        Assert.NotSame(operational, second.ServiceProvider.GetRequiredService<OperationalSqliteDbContext>());
    }

    // Disposing the session disposes its scope, and with it the contexts the stores it handed out were using.
    [Fact]
    public async Task DisposingTheSession_DisposesTheScopeAndItsContexts()
    {
        await using var composition = await GatewayComposition.CreateAsync();
        var provider = composition.Services.GetRequiredService<IStorageProvider>();

        var session = provider.CreateSession();
        var storage = session.GetStorage();
        var realm = await composition.LoadRealmAsync(storage);
        var accessTokens = storage.GetAccessTokenStore(realm);
        var realms = storage.Realms;

        session.Dispose();

        // The stores captured before disposal are backed by disposed contexts, in both families.
        await Assert.ThrowsAsync<ObjectDisposedException>(
            async () => await accessTokens.GetAsync("gateway-at", default));
        await Assert.ThrowsAsync<ObjectDisposedException>(
            async () => await realms.GetByIdAsync("gateway-realm", default));
        // The session is a lifetime, so resolving through it after disposal is an error, not a silent new scope.
        Assert.Throws<ObjectDisposedException>(session.GetStorage);
    }

    // DF3/DF21: the session is not a unit of work — a write is durable when the store completes it, with no
    // commit step, and is immediately visible to another session.
    [Fact]
    public async Task AWriteInOneSession_IsVisibleToAnother_WithoutAnyCommit()
    {
        await using var composition = await GatewayComposition.CreateAsync();
        var provider = composition.Services.GetRequiredService<IStorageProvider>();

        string realmId;
        using (var writer = provider.CreateSession())
        {
            var storage = writer.GetStorage();
            var realm = await composition.LoadRealmAsync(storage);
            realmId = realm.Id;

            var token = new AccessToken(
                "client-a", "https://issuer.contract.test", AccessTokenType.Reference, Start, 3600,
                "gateway-at", "Bearer")
            {
                RealmId = realm.Id,
            };
            await storage.GetAccessTokenStore(realm).StoreAsync(token, default);
            // No commit, no SaveChanges here: the session exposes none, by design.
        }

        using var reader = provider.CreateSession();
        var readerStorage = reader.GetStorage();
        var readerRealm = await readerStorage.Realms.GetByIdAsync(realmId, default);

        Assert.NotNull(readerRealm);
        Assert.NotNull(await readerStorage.GetAccessTokenStore(readerRealm).GetAsync("gateway-at", default));
    }

    // The provider is a singleton (it only needs the scope factory); the storage is scoped.
    [Fact]
    public async Task Provider_IsSingleton_AndStorage_IsScoped()
    {
        await using var composition = await GatewayComposition.CreateAsync();

        using var first = composition.Services.CreateScope();
        using var second = composition.Services.CreateScope();

        Assert.Same(
            first.ServiceProvider.GetRequiredService<IStorageProvider>(),
            second.ServiceProvider.GetRequiredService<IStorageProvider>());
        Assert.NotSame(
            first.ServiceProvider.GetRequiredService<IStorage>(),
            second.ServiceProvider.GetRequiredService<IStorage>());
        Assert.Same(
            first.ServiceProvider.GetRequiredService<IStorage>(),
            first.ServiceProvider.GetRequiredService<IStorage>());
    }

    // DF17: Hosted schedules the cleanup here; External schedules nothing and leaves the same maintenance for a
    // command or job — so the two schedulers can never both be running.
    [Fact]
    public async Task Hosted_RegistersExactlyOneWorker_AndExternal_RegistersNone()
    {
        await using var hosted = await GatewayComposition.CreateAsync(
            cleanup => cleanup.Mode = CleanupExecutionMode.Hosted);
        await using var external = await GatewayComposition.CreateAsync(
            cleanup => cleanup.Mode = CleanupExecutionMode.External);

        Assert.Single(hosted.Services.GetServices<IHostedService>().OfType<BackgroundService>());
        Assert.Empty(external.Services.GetServices<IHostedService>().OfType<BackgroundService>());

        // Both expose the same maintenance port: External is about who schedules it, not about having it.
        using var hostedScope = hosted.Services.CreateScope();
        using var externalScope = external.Services.CreateScope();
        Assert.NotNull(hostedScope.ServiceProvider.GetRequiredService<IOperationalMaintenance>());
        Assert.NotNull(externalScope.ServiceProvider.GetRequiredService<IOperationalMaintenance>());
    }

    // Invalid options fail at composition: a cleanup that silently never runs is the failure worth preventing.
    [Fact]
    public void Cleanup_WithInvalidOptions_FailsAtRegistration()
    {
        var services = new ServiceCollection();

        var interval = Assert.Throws<InvalidOperationException>(() =>
            services.AddEntityFrameworkOperationalCleanup(cleanup =>
            {
                cleanup.Mode = CleanupExecutionMode.Hosted;
                cleanup.Interval = TimeSpan.Zero;
            }));
        var batch = Assert.Throws<InvalidOperationException>(() =>
            services.AddEntityFrameworkOperationalCleanup(cleanup =>
            {
                cleanup.Mode = CleanupExecutionMode.External;
                cleanup.BatchSize = 0;
            }));

        Assert.Contains("Interval", interval.Message, StringComparison.Ordinal);
        Assert.Contains("BatchSize", batch.Message, StringComparison.Ordinal);
        Assert.Empty(services);
    }

    // DF17: omitting the choice is a configuration error, never a silent default on either side.
    [Fact]
    public void Cleanup_WithoutSelectingAMode_FailsAtRegistration()
    {
        var services = new ServiceCollection();

        var missing = Assert.Throws<InvalidOperationException>(() =>
            services.AddEntityFrameworkOperationalCleanup(cleanup => cleanup.BatchSize = 100));

        Assert.Contains("Mode", missing.Message, StringComparison.Ordinal);
        Assert.Empty(services);
    }

    // Selecting twice would leave the scheduler of the first call running under the options of the second.
    [Fact]
    public void Cleanup_SelectedTwice_FailsAtRegistration()
    {
        var services = new ServiceCollection();
        services.AddEntityFrameworkOperationalCleanup(cleanup => cleanup.Mode = CleanupExecutionMode.Hosted);

        var second = Assert.Throws<InvalidOperationException>(() =>
            services.AddEntityFrameworkOperationalCleanup(cleanup => cleanup.Mode = CleanupExecutionMode.External));

        Assert.Contains("already selected", second.Message, StringComparison.Ordinal);
        // The first selection stands, worker included — nothing was half-applied.
        Assert.Single(services, descriptor => descriptor.ServiceType == typeof(IHostedService));
    }

    // The validation must hold over the effective options, not over the instance the registration inspected.
    [Fact]
    public void Cleanup_ConfiguredAgainAfterRegistration_FailsWhenTheOptionsAreResolved()
    {
        var services = new ServiceCollection();
        services.AddEntityFrameworkOperationalCleanup(cleanup => cleanup.Mode = CleanupExecutionMode.Hosted);
        // A later Configure that flips the mode: the worker is registered but the options would say otherwise.
        services.Configure<OperationalCleanupOptions>(cleanup => cleanup.Mode = CleanupExecutionMode.External);

        using var provider = services.BuildServiceProvider();

        var failure = Assert.Throws<OptionsValidationException>(
            () => provider.GetRequiredService<IOptions<OperationalCleanupOptions>>().Value);

        Assert.Contains("registered 'Hosted'", failure.Message, StringComparison.Ordinal);
    }

    // The complete gateway is a production composition: persisting operational data with no scheduler at all is
    // not something to discover in production.
    [Fact]
    public void Gateway_WithoutASelectedCleanupMode_RefusesToCompose()
    {
        var services = new ServiceCollection();

        var refusal = Assert.Throws<InvalidOperationException>(services.AddEntityFrameworkStorage);

        Assert.Contains(nameof(CleanupExecutionMode.Hosted), refusal.Message, StringComparison.Ordinal);
        Assert.Contains(nameof(CleanupExecutionMode.External), refusal.Message, StringComparison.Ordinal);
        Assert.Empty(services);
    }

    // The maintenance port is also used directly by an external command/job, without the complete IStorage
    // gateway. Registering the Operational stores alone must not expose a cleanup that never selected its mode.
    [Fact]
    public void OperationalStorage_WithoutASelectedCleanupMode_DoesNotExposeMaintenance()
    {
        var services = new ServiceCollection();

        services.AddEntityFrameworkOperationalStorage<OperationalSqliteDbContext>();

        Assert.DoesNotContain(
            services,
            descriptor => descriptor.ServiceType == typeof(IOperationalMaintenance));

        services.AddEntityFrameworkOperationalCleanup(cleanup =>
            cleanup.Mode = CleanupExecutionMode.External);

        Assert.Contains(
            services,
            descriptor => descriptor.ServiceType == typeof(IOperationalMaintenance));
    }

    /// <summary>
    /// The production composition under test: both EF families over one SQLite database, the snapshot bootstrapped
    /// like the host does, and the gateway added by its public opt-in.
    /// </summary>
    public sealed class GatewayComposition : IAsyncDisposable
    {
        public const string RealmAId = "gateway-realm";
        public const string RealmBId = "gateway-realm-b";

        private readonly SqliteOperationalDatabase database;

        private GatewayComposition(
            SqliteOperationalDatabase database, ServiceProvider services, CommandCounter commands)
        {
            this.database = database;
            Services = services;
            Commands = commands;
        }

        public ServiceProvider Services { get; }

        internal CommandCounter Commands { get; }

        public static async Task<GatewayComposition> CreateAsync(Action<OperationalCleanupOptions>? cleanup = null)
        {
            var database = await SqliteOperationalDatabase.CreateMigratedAsync();
            var commands = new CommandCounter();
            var clock = new FakeClock(Start);

            var collection = new ServiceCollection();
            collection.AddLogging();
            collection.AddSingleton<TimeProvider>(clock);
            collection.AddSingleton(new ConfigurationSnapshotRefreshOptions
            {
                RefreshInterval = TimeSpan.FromMinutes(5),
            });

            collection.AddDbContext<ConfigurationSqliteDbContext>(options => options
                .UseSqlite(database.Connection, sqlite => sqlite.UseConfigurationMigrationsHistory())
                .AddInterceptors(commands));
            collection.AddDbContext<OperationalSqliteDbContext>(options => options
                .UseSqlite(database.Connection, sqlite => sqlite.UseOperationalMigrationsHistory())
                .AddInterceptors(commands));

            collection.AddEntityFrameworkConfigurationStorage<ConfigurationSqliteDbContext>();
            collection.AddEntityFrameworkConfigurationSnapshotSource();
            collection.AddConfigurationSnapshot();
            collection.AddEntityFrameworkOperationalStorage<OperationalSqliteDbContext>();
            collection.AddOperationalAesGcmPayloadProtection(
                OperationalStorageOptions.DefaultPayloadProtectionProfile, [.. GatewayProtectorKey]);
            collection.AddAesKeyMaterialProtector(options => options.Key = [.. GatewayProtectorKey]);

            // The gateway refuses to compose without an explicit choice, so the fixture makes one.
            collection.AddEntityFrameworkOperationalCleanup(
                cleanup ?? (options => options.Mode = CleanupExecutionMode.External));

            collection.AddEntityFrameworkStorage();

            var services = collection.BuildServiceProvider(new ServiceProviderOptions
            {
                ValidateOnBuild = true,
                ValidateScopes = true,
            });

            try
            {
                await SeedAsync(services, database);
                // The host bootstraps the snapshot before traffic; the gateway's ServerOptions depends on it.
                await services.GetRequiredService<IConfigurationSnapshotRefresher>().RefreshAsync();

                return new GatewayComposition(database, services, commands);
            }
            catch
            {
                await services.DisposeAsync();
                await database.DisposeAsync();
                throw;
            }
        }

        /// <summary>Reads the seeded realm back through the gateway, which is how a caller would get it.</summary>
        public async Task<Realm> LoadRealmAsync(IStorage storage, string realmId = RealmAId)
            => await storage.Realms.GetByIdAsync(realmId, default)
                ?? throw new InvalidOperationException($"The gateway fixture realm '{realmId}' is missing.");

        private static async Task SeedAsync(IServiceProvider services, SqliteOperationalDatabase database)
        {
            using var scope = services.CreateScope();
            var serverOptions = new ServerOptions();
            var serverSerializer = scope.ServiceProvider
                .GetRequiredService<RoyalIdentity.Storage.EntityFramework.Configuration.Materialization.ServerOptionsPayloadSerializer>();
            var payload = serverSerializer.Serialize(serverOptions);

            await using var context = database.NewConfigurationContext();
            context.ServerOptions.Add(new ServerOptionsEntity
            {
                Id = ServerOptionsEntity.SingletonId,
                PayloadVersion = payload.Version,
                PayloadJson = payload.Json,
                UpdatedAtUtc = Start,
            });
            await context.SaveChangesAsync();

            var realmAOptions = new RealmOptions(serverOptions)
            {
                OperationalStorage =
                {
                    JwtAccessTokenPersistence = JwtAccessTokenPersistenceMode.Full,
                },
            };
            var realmBOptions = new RealmOptions(serverOptions)
            {
                OperationalStorage =
                {
                    JwtAccessTokenPersistence = JwtAccessTokenPersistenceMode.Full,
                },
            };
            var realmA = new Realm(
                RealmAId, "gateway.contract.test", "gateway", "Gateway Realm", false, realmAOptions);
            var realmB = new Realm(
                RealmBId, "gateway-b.contract.test", "gateway-b", "Gateway Realm B", false, realmBOptions);

            var realms = scope.ServiceProvider.GetRequiredService<IRealmStore>();
            await realms.SaveAsync(realmA);
            await realms.SaveAsync(realmB);
        }

        public async ValueTask DisposeAsync()
        {
            await Services.DisposeAsync();
            await database.DisposeAsync();
        }

        private static ReadOnlySpan<byte> GatewayProtectorKey
            =>
            [
                0x31, 0x32, 0x33, 0x34, 0x35, 0x36, 0x37, 0x38,
                0x39, 0x3A, 0x3B, 0x3C, 0x3D, 0x3E, 0x3F, 0x40,
                0x41, 0x42, 0x43, 0x44, 0x45, 0x46, 0x47, 0x48,
                0x49, 0x4A, 0x4B, 0x4C, 0x4D, 0x4E, 0x4F, 0x50,
            ];
    }

    /// <summary>Counts commands actually sent to the database, so "opens no connection" is an assertion.</summary>
    internal sealed class CommandCounter : DbCommandInterceptor
    {
        private int count;

        public int Count => Volatile.Read(ref count);

        public void Reset() => Volatile.Write(ref count, 0);

        public override InterceptionResult<DbDataReader> ReaderExecuting(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
        {
            Interlocked.Increment(ref count);
            return base.ReaderExecuting(command, eventData, result);
        }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref count);
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }

        public override InterceptionResult<int> NonQueryExecuting(
            DbCommand command, CommandEventData eventData, InterceptionResult<int> result)
        {
            Interlocked.Increment(ref count);
            return base.NonQueryExecuting(command, eventData, result);
        }

        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref count);
            return base.NonQueryExecutingAsync(command, eventData, result, cancellationToken);
        }
    }
}
