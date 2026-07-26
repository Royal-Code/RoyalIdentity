using Microsoft.EntityFrameworkCore;
using RoyalIdentity.Contracts.Storage;
using RoyalIdentity.Data.Configuration.Entities;
using RoyalIdentity.Data.Operational;
using RoyalIdentity.Models;
using RoyalIdentity.Storage.EntityFramework.Operational.Maintenance;
using RoyalIdentity.Storage.EntityFramework.Operational.Stores;
using Tests.Storage.Support;

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

    /// <summary>The controllable clock the backing was composed with.</summary>
    FakeClock Clock { get; }

    /// <summary>The harness scope, so a scenario can resolve the ports the provider registered.</summary>
    IServiceProvider ScopedServices { get; }

    /// <summary>The profile the fixture writes with by default.</summary>
    string DefaultProfile { get; }

    /// <summary>An independently keyed profile, so realm isolation of protection is observable.</summary>
    string AlternateProfile { get; }

    /// <summary>A fresh context for inspecting rows — never the scoped one the stores use.</summary>
    OperationalDbContext NewOperationalContext();

    /// <summary>
    /// Counts rows of a Configuration table, so a purge scenario can prove the other family was untouched.
    /// Both families share one database in these fixtures, which is what makes that assertion real.
    /// </summary>
    Task<int> CountConfigurationAsync<TEntity>() where TEntity : class;
}

internal sealed class SqliteParityHarness(SqliteOperationalStorageHarness harness) : IOperationalParityHarness
{
    public IStorage Storage => harness.Storage;

    public Realm RealmA => harness.RealmA;

    public Realm RealmB => harness.RealmB;

    public FakeClock Clock => harness.Clock;

    public IServiceProvider ScopedServices => harness.ScopedServices;

    public string DefaultProfile => SqliteOperationalStorageHarness.DefaultProtectionProfile;

    public string AlternateProfile => SqliteOperationalStorageHarness.AlternateProtectionProfile;

    public OperationalDbContext NewOperationalContext() => harness.NewOperationalContext();

    public async Task<int> CountConfigurationAsync<TEntity>() where TEntity : class
        => await harness.DbContext.Set<TEntity>().AsNoTracking().CountAsync();

    public async ValueTask DisposeAsync() => await harness.DisposeAsync();

    public static async Task<IOperationalParityHarness> CreateAsync(
        IAuthorizeParametersHandleGenerator? handleGenerator = null,
        Action<OperationalCleanupOptions>? cleanup = null)
        => new SqliteParityHarness(
            await SqliteOperationalStorageHarness.CreateConcreteAsync(handleGenerator, cleanup));
}

internal sealed class PostgreSqlParityHarness(PostgreSqlOperationalStorageHarness harness) : IOperationalParityHarness
{
    public IStorage Storage => harness.Storage;

    public Realm RealmA => harness.RealmA;

    public Realm RealmB => harness.RealmB;

    public FakeClock Clock => harness.Clock;

    public IServiceProvider ScopedServices => harness.ScopedServices;

    public string DefaultProfile => PostgreSqlOperationalStorageHarness.DefaultProtectionProfile;

    public string AlternateProfile => PostgreSqlOperationalStorageHarness.AlternateProtectionProfile;

    public OperationalDbContext NewOperationalContext() => harness.NewOperationalContext();

    public async Task<int> CountConfigurationAsync<TEntity>() where TEntity : class
        => await harness.DbContext.Set<TEntity>().AsNoTracking().CountAsync();

    public async ValueTask DisposeAsync() => await harness.DisposeAsync();

    public static async Task<IOperationalParityHarness> CreateAsync(
        IAuthorizeParametersHandleGenerator? handleGenerator = null,
        Action<OperationalCleanupOptions>? cleanup = null)
        => new PostgreSqlParityHarness(
            await PostgreSqlOperationalStorageHarness.CreateConcreteAsync(handleGenerator, cleanup));
}

/// <summary>
/// The shape every provider-parity suite takes: one abstract suite, a SQLite specialization that runs
/// unconditionally, and a PostgreSQL one driven by an opt-in aggregate. Kept here so the suites themselves only
/// declare their scenarios.
/// </summary>
internal static class ParityHarnessAssertions
{
    /// <summary>Guards against a fixture whose Configuration side is missing, which would make purge assertions vacuous.</summary>
    public static async Task AssertConfigurationIsPopulatedAsync(IOperationalParityHarness harness)
        => Assert.True(
            await harness.CountConfigurationAsync<RealmEntity>() > 0,
            "the fixture must have Configuration realms for the isolation assertion to mean anything");
}
