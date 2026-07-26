using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RoyalIdentity.Configuration;
using RoyalIdentity.Contracts.Storage;
using RoyalIdentity.Data.Configuration.Entities;
using RoyalIdentity.Models;
using RoyalIdentity.Models.Tokens;
using RoyalIdentity.Options;
using RoyalIdentity.Storage.EntityFramework.Configuration.Materialization;
using RoyalIdentity.Storage.EntityFramework.Extensions;
using RoyalIdentity.Storage.EntityFramework.Operational.Maintenance;
using RoyalIdentity.Storage.EntityFramework.Operational.Stores;
using RoyalIdentity.Storage.EntityFramework.PostgreSql;
using Tests.Storage.Configuration;
using Tests.Storage.Operational.Support;
using Tests.Storage.Support;

namespace Tests.Storage.Operational;

/// <summary>
/// The complete EF gateway over PostgreSQL with both families in <b>one</b> database (plan Fase 7, DF21/DF22).
/// That is the topology worth proving: sharing a database must not turn the two families into one unit of work,
/// and each must keep its own context and connection.
/// </summary>
public class PostgreSqlStorageGatewayTests
{
    private static readonly DateTime Start = StorageContractHarness.Start;

    [StoragePostgreSqlFact]
    [Trait("Category", "PostgreSql")]
    public async Task Gateway_ResolvesEveryMember_AndExposesTheAtomicCapabilities()
    {
        await using var composition = await GatewayComposition.CreateAsync();
        using var scope = composition.Services.CreateScope();
        var storage = scope.ServiceProvider.GetRequiredService<IStorage>();
        var realm = await composition.LoadRealmAsync(storage);

        Assert.NotNull(storage.ServerOptions);
        Assert.NotNull(storage.GetClientStore(realm));
        Assert.NotNull(storage.GetKeyStore(realm));
        Assert.NotNull(storage.GetResourceStore(realm));
        Assert.NotNull(storage.GetAccessTokenStore(realm));
        Assert.NotNull(storage.GetUserConsentStore(realm));
        Assert.NotNull(storage.GetUserSessionStore(realm));
        Assert.NotNull(storage.GetAuthorizeParametersStore(realm));

        // DF46: the capabilities MP-2/MP-3 are guaranteed at compile time on every provider.
        Assert.IsAssignableFrom<IOperationalAuthorizationCodeStore>(storage.GetAuthorizationCodeStore(realm));
        Assert.IsAssignableFrom<IOperationalRefreshTokenStore>(storage.GetRefreshTokenStore(realm));
    }

    // DF2/DF3: one database, still two contexts and two connections — and no transaction spanning them.
    [StoragePostgreSqlFact]
    [Trait("Category", "PostgreSql")]
    public async Task WithBothFamiliesInOneDatabase_TheScopeKeepsTwoContextsAndTwoConnections()
    {
        await using var composition = await GatewayComposition.CreateAsync();
        using var scope = composition.Services.CreateScope();

        var configuration = scope.ServiceProvider.GetRequiredService<ConfigurationPostgreSqlDbContext>();
        var operational = scope.ServiceProvider.GetRequiredService<OperationalPostgreSqlDbContext>();

        Assert.NotSame((DbContext)configuration, operational);
        Assert.NotSame(configuration.ChangeTracker, operational.ChangeTracker);
        Assert.NotSame(configuration.Database.GetDbConnection(), operational.Database.GetDbConnection());
        // Neither family enlisted the other in a transaction: there is none to share.
        Assert.Null(configuration.Database.CurrentTransaction);
        Assert.Null(operational.Database.CurrentTransaction);
    }

    // The session is a lifetime, not a unit of work: a write is durable when the store completes it.
    [StoragePostgreSqlFact]
    [Trait("Category", "PostgreSql")]
    public async Task AWriteInOneSession_IsVisibleToAnother_WithoutAnyCommit()
    {
        await using var composition = await GatewayComposition.CreateAsync();
        var provider = composition.Services.GetRequiredService<IStorageProvider>();

        using (var writer = provider.CreateSession())
        {
            var storage = writer.GetStorage();
            var realm = await composition.LoadRealmAsync(storage);
            var token = new AccessToken(
                "client-a", "https://issuer.contract.test", AccessTokenType.Reference, Start, 3600,
                "gateway-at", "Bearer")
            {
                RealmId = realm.Id,
            };
            await storage.GetAccessTokenStore(realm).StoreAsync(token, default);
        }

        using var reader = provider.CreateSession();
        var readerStorage = reader.GetStorage();
        var readerRealm = await composition.LoadRealmAsync(readerStorage);

        Assert.NotNull(await readerStorage.GetAccessTokenStore(readerRealm).GetAsync("gateway-at", default));
    }

    // Disposal is a real resource concern on PostgreSQL, where a leaked scope means a leaked connection.
    [StoragePostgreSqlFact]
    [Trait("Category", "PostgreSql")]
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
        Assert.Throws<ObjectDisposedException>(session.GetStorage);
    }

    /// <summary>The production composition under test: both EF families over one PostgreSQL database.</summary>
    private sealed class GatewayComposition : IAsyncDisposable
    {
        private readonly PostgreSqlOperationalDatabase database;

        private GatewayComposition(PostgreSqlOperationalDatabase database, ServiceProvider services)
        {
            this.database = database;
            Services = services;
        }

        public ServiceProvider Services { get; }

        public static async Task<GatewayComposition> CreateAsync()
        {
            var database = await PostgreSqlOperationalDatabase.CreateMigratedAsync();
            var clock = new FakeClock(Start);

            var collection = new ServiceCollection();
            collection.AddLogging();
            collection.AddSingleton<TimeProvider>(clock);
            collection.AddSingleton(new ConfigurationSnapshotRefreshOptions
            {
                RefreshInterval = TimeSpan.FromMinutes(5),
            });

            collection.AddDbContext<ConfigurationPostgreSqlDbContext>(options => options
                .UseNpgsql(database.ConnectionString, npgsql => npgsql.UseConfigurationMigrationsHistory()));
            collection.AddDbContext<OperationalPostgreSqlDbContext>(options => options
                .UseNpgsql(database.ConnectionString, npgsql => npgsql.UseOperationalMigrationsHistory()));

            collection.AddEntityFrameworkConfigurationStorage<ConfigurationPostgreSqlDbContext>();
            collection.AddEntityFrameworkConfigurationSnapshotSource();
            collection.AddConfigurationSnapshot();
            collection.AddEntityFrameworkOperationalStorage<OperationalPostgreSqlDbContext>();
            collection.AddOperationalAesGcmPayloadProtection(
                OperationalStorageOptions.DefaultPayloadProtectionProfile, [.. GatewayProtectorKey]);
            collection.AddAesKeyMaterialProtector(options => options.Key = [.. GatewayProtectorKey]);
            collection.AddEntityFrameworkOperationalCleanup(
                cleanup => cleanup.Mode = CleanupExecutionMode.External);
            collection.AddEntityFrameworkStorage();

            var services = collection.BuildServiceProvider(new ServiceProviderOptions
            {
                ValidateOnBuild = true,
                ValidateScopes = true,
            });

            try
            {
                await SeedAsync(services, database);
                await services.GetRequiredService<IConfigurationSnapshotRefresher>().RefreshAsync();

                return new GatewayComposition(database, services);
            }
            catch
            {
                await services.DisposeAsync();
                await database.DisposeAsync();
                throw;
            }
        }

        public async Task<Realm> LoadRealmAsync(IStorage storage)
            => await storage.Realms.GetByIdAsync("gateway-realm", default)
                ?? throw new InvalidOperationException("The gateway fixture realm is missing.");

        private static async Task SeedAsync(IServiceProvider services, PostgreSqlOperationalDatabase database)
        {
            using var scope = services.CreateScope();
            var serverOptions = new ServerOptions();
            var payload = scope.ServiceProvider
                .GetRequiredService<ServerOptionsPayloadSerializer>()
                .Serialize(serverOptions);

            await using (var context = database.NewConfigurationContext())
            {
                context.ServerOptions.Add(new ServerOptionsEntity
                {
                    Id = ServerOptionsEntity.SingletonId,
                    PayloadVersion = payload.Version,
                    PayloadJson = payload.Json,
                    UpdatedAtUtc = Start,
                });
                await context.SaveChangesAsync();
            }

            var realmOptions = new RealmOptions(serverOptions)
            {
                OperationalStorage = { JwtAccessTokenPersistence = JwtAccessTokenPersistenceMode.Full },
            };
            var realm = new Realm(
                "gateway-realm", "gateway.contract.test", "gateway", "Gateway Realm", false, realmOptions);

            await scope.ServiceProvider.GetRequiredService<IRealmStore>().SaveAsync(realm);
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
}
