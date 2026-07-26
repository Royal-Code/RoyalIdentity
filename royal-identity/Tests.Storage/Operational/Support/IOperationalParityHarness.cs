using RoyalIdentity.Contracts.Storage;
using RoyalIdentity.Data.Operational;
using RoyalIdentity.Models;

namespace Tests.Storage.Operational.Support;

/// <summary>
/// The slice of an Operational fixture a provider-parity scenario needs. It deliberately exposes the base
/// <see cref="OperationalDbContext"/> rather than a provider context: a scenario that asserts agreement between
/// providers must not be able to reach anything provider-specific.
/// </summary>
internal interface IOperationalParityHarness : IAsyncDisposable
{
    IStorage Storage { get; }

    Realm RealmA { get; }

    Realm RealmB { get; }

    /// <summary>The profile the fixture writes with by default.</summary>
    string DefaultProfile { get; }

    /// <summary>An independently keyed profile, so realm isolation of protection is observable.</summary>
    string AlternateProfile { get; }

    /// <summary>A fresh context for inspecting rows — never the scoped one the stores use.</summary>
    OperationalDbContext NewOperationalContext();
}

internal sealed class SqliteParityHarness(SqliteOperationalStorageHarness harness) : IOperationalParityHarness
{
    public IStorage Storage => harness.Storage;

    public Realm RealmA => harness.RealmA;

    public Realm RealmB => harness.RealmB;

    public string DefaultProfile => SqliteOperationalStorageHarness.DefaultProtectionProfile;

    public string AlternateProfile => SqliteOperationalStorageHarness.AlternateProtectionProfile;

    public OperationalDbContext NewOperationalContext() => harness.NewOperationalContext();

    public async ValueTask DisposeAsync() => await harness.DisposeAsync();

    public static async Task<IOperationalParityHarness> CreateAsync()
        => new SqliteParityHarness(await SqliteOperationalStorageHarness.CreateConcreteAsync());
}

internal sealed class PostgreSqlParityHarness(PostgreSqlOperationalStorageHarness harness) : IOperationalParityHarness
{
    public IStorage Storage => harness.Storage;

    public Realm RealmA => harness.RealmA;

    public Realm RealmB => harness.RealmB;

    public string DefaultProfile => PostgreSqlOperationalStorageHarness.DefaultProtectionProfile;

    public string AlternateProfile => PostgreSqlOperationalStorageHarness.AlternateProtectionProfile;

    public OperationalDbContext NewOperationalContext() => harness.NewOperationalContext();

    public async ValueTask DisposeAsync() => await harness.DisposeAsync();

    public static async Task<IOperationalParityHarness> CreateAsync()
        => new PostgreSqlParityHarness(await PostgreSqlOperationalStorageHarness.CreateConcreteAsync());
}
