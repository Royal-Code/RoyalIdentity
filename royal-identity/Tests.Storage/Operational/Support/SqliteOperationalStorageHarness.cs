using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RoyalIdentity.Options;
using RoyalIdentity.Storage.EntityFramework.Extensions;
using RoyalIdentity.Storage.EntityFramework.Operational.Stores;
using RoyalIdentity.Storage.EntityFramework.Sqlite;
using Tests.Storage.Configuration.Support;
using Tests.Storage.Support;

namespace Tests.Storage.Operational.Support;

/// <summary>
/// Contract harness backed by EF for both families over one SQLite database: Configuration from Plano 2 plus
/// the Operational stores each phase delivers. The provider-neutral contract suite runs against it unchanged;
/// the Operational acceptances that only EF can satisfy live beside it.
/// </summary>
internal sealed class SqliteOperationalStorageHarness
    : ConfigurationStorageHarness<ConfigurationSqliteDbContext>
{
    /// <summary>Profile the fixture writes with; a second one exists so rotation/isolation can be exercised.</summary>
    public const string DefaultProtectionProfile = OperationalStorageOptions.DefaultPayloadProtectionProfile;

    /// <summary>An independently keyed profile, for scenarios that give two realms different protection.</summary>
    public const string AlternateProtectionProfile = "contract-alternate";

    /// <summary>An explicitly registered unprotected profile, never a fallback (plan DF30).</summary>
    public const string PlainProtectionProfile = "contract-plain";

    private readonly SqliteOperationalDatabase database;

    private SqliteOperationalStorageHarness(HarnessState state) : base(state)
        => database = (SqliteOperationalDatabase)state.Database;

    internal IOperationalStoreFactory OperationalStores
        => ScopedServices.GetRequiredService<IOperationalStoreFactory>();

    /// <summary>
    /// A fresh Operational context for inspecting rows. It is deliberately not the scoped context the stores
    /// use: a scenario asserting what was persisted must read the database, not a change tracker.
    /// </summary>
    internal OperationalSqliteDbContext NewOperationalContext() => database.NewOperationalContext();

    public static async Task<StorageContractHarness> CreateAsync() => await CreateConcreteAsync();

    internal static async Task<SqliteOperationalStorageHarness> CreateConcreteAsync()
    {
        var database = await SqliteOperationalDatabase.CreateMigratedAsync();

        return await CreateCoreAsync(
            database,
            services =>
            {
                services.AddDbContext<ConfigurationSqliteDbContext>(options => options
                    .UseSqlite(database.Connection, sqlite => sqlite.UseConfigurationMigrationsHistory()));
                services.AddEntityFrameworkConfigurationStorage<ConfigurationSqliteDbContext>();

                services.AddDbContext<OperationalSqliteDbContext>(options => options
                    .UseSqlite(database.Connection, sqlite => sqlite.UseOperationalMigrationsHistory()));
                services.AddEntityFrameworkOperationalStorage<OperationalSqliteDbContext>();

                // The Plain profile warns on construction, so the fixture needs logging registered.
                services.AddLogging();
                services.AddOperationalAesGcmPayloadProtection(DefaultProtectionProfile, [.. TestProtectorKey]);
                services.AddOperationalAesGcmPayloadProtection(AlternateProtectionProfile, [.. AlternateProtectorKey]);
                services.AddOperationalPlainPayloadProtection(PlainProtectionProfile);
            },
            state => new SqliteOperationalStorageHarness(state),
            ApplyOperationalDefaults);
    }

    /// <inheritdoc />
    protected override void ConfigureRealmOptions(RealmOptions options) => ApplyOperationalDefaults(options);

    /// <summary>
    /// The contract suite issues JWT access tokens, and a realm at product defaults persists none of them
    /// (<see cref="JwtAccessTokenPersistenceMode.None"/> — plan DF31). The contracts describe the store, so the
    /// fixture composes a realm that persists them; the None/Metadata/Full behaviors themselves are covered by
    /// dedicated acceptances that set the mode explicitly.
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
