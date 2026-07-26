using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RoyalIdentity.Options;
using RoyalIdentity.Storage.EntityFramework.Extensions;
using RoyalIdentity.Storage.EntityFramework.Operational.Maintenance;
using RoyalIdentity.Storage.EntityFramework.Operational.Stores;
using RoyalIdentity.Storage.EntityFramework.PostgreSql;
using Tests.Storage.Configuration.Support;
using Tests.Storage.Support;

namespace Tests.Storage.Operational.Support;

/// <summary>
/// The PostgreSQL twin of <see cref="SqliteOperationalStorageHarness"/>: both families over one PostgreSQL
/// database, composed exactly the same way. The provider-neutral contract suite runs against it unchanged,
/// which is what makes "SQLite and PostgreSQL agree" a verified claim rather than an intention (plan Fase 7).
/// </summary>
internal sealed class PostgreSqlOperationalStorageHarness
    : ConfigurationStorageHarness<ConfigurationPostgreSqlDbContext>
{
    /// <summary>Profile the fixture writes with; a second one exists so rotation/isolation can be exercised.</summary>
    public const string DefaultProtectionProfile = OperationalStorageOptions.DefaultPayloadProtectionProfile;

    /// <summary>An independently keyed profile, for scenarios that give two realms different protection.</summary>
    public const string AlternateProtectionProfile = "contract-alternate";

    /// <summary>An explicitly registered unprotected profile, never a fallback (plan DF30).</summary>
    public const string PlainProtectionProfile = "contract-plain";

    private readonly PostgreSqlOperationalDatabase database;

    private PostgreSqlOperationalStorageHarness(HarnessState state) : base(state)
        => database = (PostgreSqlOperationalDatabase)state.Database;

    internal IOperationalStoreFactory OperationalStores
        => ScopedServices.GetRequiredService<IOperationalStoreFactory>();

    /// <summary>
    /// A fresh Operational context for inspecting rows. It is deliberately not the scoped context the stores
    /// use: a scenario asserting what was persisted must read the database, not a change tracker.
    /// </summary>
    internal OperationalPostgreSqlDbContext NewOperationalContext() => database.NewOperationalContext();

    public static async Task<StorageContractHarness> CreateAsync() => await CreateConcreteAsync();

    /// <param name="handleGenerator">
    /// Replaces the authorize-parameters handle generator, so a scenario can stage a collision instead of
    /// waiting for one (plan DF16).
    /// </param>
    /// <param name="cleanup">Configures the maintenance options this fixture exposes.</param>
    internal static async Task<PostgreSqlOperationalStorageHarness> CreateConcreteAsync(
        IAuthorizeParametersHandleGenerator? handleGenerator = null,
        Action<OperationalCleanupOptions>? cleanup = null)
    {
        var database = await PostgreSqlOperationalDatabase.CreateMigratedAsync();

        return await CreateCoreAsync(
            database,
            services =>
            {
                if (handleGenerator is not null)
                    services.AddSingleton(handleGenerator);

                services.AddDbContext<ConfigurationPostgreSqlDbContext>(options => options
                    .UseNpgsql(database.ConnectionString, npgsql => npgsql.UseConfigurationMigrationsHistory()));
                services.AddEntityFrameworkConfigurationStorage<ConfigurationPostgreSqlDbContext>();

                services.AddDbContext<OperationalPostgreSqlDbContext>(options => options
                    .UseNpgsql(database.ConnectionString, npgsql => npgsql.UseOperationalMigrationsHistory()));
                services.AddEntityFrameworkOperationalStorage<OperationalPostgreSqlDbContext>();
                services.AddEntityFrameworkOperationalCleanup(options =>
                {
                    options.Mode = CleanupExecutionMode.External;
                    cleanup?.Invoke(options);
                });

                // The Plain profile warns on construction, so the fixture needs logging registered.
                services.AddLogging();
                services.AddOperationalAesGcmPayloadProtection(DefaultProtectionProfile, [.. TestProtectorKey]);
                services.AddOperationalAesGcmPayloadProtection(AlternateProtectionProfile, [.. AlternateProtectorKey]);
                services.AddOperationalPlainPayloadProtection(PlainProtectionProfile);
            },
            state => new PostgreSqlOperationalStorageHarness(state),
            ApplyOperationalDefaults);
    }

    /// <inheritdoc />
    protected override void ConfigureRealmOptions(RealmOptions options) => ApplyOperationalDefaults(options);

    /// <summary>
    /// The contract suite issues JWT access tokens, and a realm at product defaults persists none of them
    /// (plan DF31). The contracts describe the store, so the fixture composes a realm that persists them.
    /// </summary>
    private static void ApplyOperationalDefaults(RealmOptions options)
    {
        options.OperationalStorage.JwtAccessTokenPersistence = JwtAccessTokenPersistenceMode.Full;
        options.OperationalStorage.PayloadProtectionProfile = DefaultProtectionProfile;
    }

    private static ReadOnlySpan<byte> AlternateProtectorKey
        =>
        [
            0x20, 0x1F, 0x1E, 0x1D, 0x1C, 0x1B, 0x1A, 0x19,
            0x18, 0x17, 0x16, 0x15, 0x14, 0x13, 0x12, 0x11,
            0x10, 0x0F, 0x0E, 0x0D, 0x0C, 0x0B, 0x0A, 0x09,
            0x08, 0x07, 0x06, 0x05, 0x04, 0x03, 0x02, 0x01,
        ];
}
