using Microsoft.EntityFrameworkCore;
using RoyalIdentity.Data.Operational;
using RoyalIdentity.Storage.EntityFramework.PostgreSql;
using RoyalIdentity.Storage.EntityFramework.Sqlite;
using RoyalIdentity.UserAccounts.Infrastructure.Data;
using RoyalIdentity.UserAccounts.Infrastructure.Events;
using RoyalIdentity.UserAccounts.PostgreSql;
using RoyalIdentity.UserAccounts.Sqlite;

namespace RoyalIdentity.Migrations;

/// <summary>What happened to one family in a run.</summary>
public enum StorageMigrationStatus
{
    /// <summary>The family was not selected by this run.</summary>
    Skipped,

    /// <summary>Migrations were applied (or were already up to date).</summary>
    Applied,

    /// <summary>The family failed; its migrations may be partially applied, exactly as EF left them.</summary>
    Failed,

    /// <summary>An earlier family failed, so this one was never started.</summary>
    NotAttempted,
}

/// <summary>The outcome of one family, reported on its own (plan DF23).</summary>
public sealed record StorageMigrationFamilyResult(
    StorageFamilySelection Family,
    StorageMigrationStatus Status,
    Exception? Failure = null);

/// <summary>
/// The outcome of a run, family by family. There is deliberately no aggregate "rolled back": the families may
/// live in different databases, and even in one database they are applied as independent sequences, so a
/// failure in one says nothing about the other beyond what these results state.
/// </summary>
public sealed record StorageMigrationReport(IReadOnlyList<StorageMigrationFamilyResult> Families)
{
    public bool Succeeded => Families.All(result => result.Status is not StorageMigrationStatus.Failed);

    public StorageMigrationFamilyResult For(StorageFamilySelection family)
        => Families.Single(result => result.Family == family);
}

/// <summary>
/// Applies migrations for the selected storage families (plan DF23). Each family carries its own migrations
/// history, so they are applied as independent sequences even when the composition points them at one database —
/// Configuration first, then Operational, then UserAccounts, never inside a shared transaction.
/// </summary>
public static class StorageMigrationRunner
{
    public static async Task<StorageMigrationReport> RunAsync(
        MigrationRunnerOptions options, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.ValidateDatabaseTopology();

        List<StorageMigrationFamilyResult> results = [];
        var stopped = false;

        // Whether a failure in one family stops the next. This is driven by the declared physical topology,
        // never inferred from connection-string equality: one database may use distinct credentials for each
        // family. Over one database a failure leaves that database in an unknown state. Over separate databases,
        // coupling them would be exactly the joint atomicity this plan says does not exist (DF21/DF23).
        var failureStopsTheRun = options.SharesOneDatabase;

        foreach (var family in new[]
        {
            StorageFamilySelection.Configuration,
            StorageFamilySelection.Operational,
            StorageFamilySelection.UserAccounts,
        })
        {
            if (!options.Families.HasFlag(family))
            {
                results.Add(new StorageMigrationFamilyResult(family, StorageMigrationStatus.Skipped));
                continue;
            }

            if (stopped)
            {
                results.Add(new StorageMigrationFamilyResult(family, StorageMigrationStatus.NotAttempted));
                continue;
            }

            try
            {
                await RunFamilyAsync(family, options, ct);

                results.Add(new StorageMigrationFamilyResult(family, StorageMigrationStatus.Applied));
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                results.Add(new StorageMigrationFamilyResult(family, StorageMigrationStatus.Failed, exception));
                stopped = failureStopsTheRun;
            }
        }

        return new StorageMigrationReport(results);
    }

    private static Task RunFamilyAsync(
        StorageFamilySelection family,
        MigrationRunnerOptions options,
        CancellationToken ct)
        => family switch
        {
            StorageFamilySelection.Configuration => ConfigurationMigrationRunner.RunAsync(options, ct),
            StorageFamilySelection.Operational => RunOperationalAsync(options, ct),
            StorageFamilySelection.UserAccounts => RunUserAccountsAsync(options, ct),
            _ => throw new InvalidOperationException("Unsupported storage family."),
        };

    /// <summary>
    /// Operational has no seed and no legacy history to relocate: it never shipped under EF's default history,
    /// and its rows are produced only by live protocol flows (plan DF19/DF23).
    /// </summary>
    private static async Task RunOperationalAsync(MigrationRunnerOptions options, CancellationToken ct)
    {
        await using var context = CreateOperationalContext(options);
        await context.Database.MigrateAsync(ct);
    }

    private static OperationalDbContext CreateOperationalContext(MigrationRunnerOptions options)
        => options.ConfigurationProvider switch
        {
            ConfigurationDatabaseProvider.Sqlite => new OperationalSqliteDbContext(
                new DbContextOptionsBuilder<OperationalSqliteDbContext>()
                    .UseSqlite(
                        options.ResolvedOperationalConnection,
                        sqlite => sqlite.UseOperationalMigrationsHistory())
                    .Options),
            ConfigurationDatabaseProvider.PostgreSql => new OperationalPostgreSqlDbContext(
                new DbContextOptionsBuilder<OperationalPostgreSqlDbContext>()
                    .UseNpgsql(
                        options.ResolvedOperationalConnection,
                        npgsql => npgsql.UseOperationalMigrationsHistory())
                    .Options),
            _ => throw new InvalidOperationException("Unsupported Operational provider."),
        };

    /// <summary>
    /// UserAccounts owns its schema and migrations independently from the IdP storage gateway. The migration
    /// context has no domain work to dispatch, so the module dispatcher intentionally has no observers.
    /// </summary>
    private static async Task RunUserAccountsAsync(MigrationRunnerOptions options, CancellationToken ct)
    {
        await using var context = CreateUserAccountsContext(options);
        var migrations = context.Database.GetMigrations().ToHashSet(StringComparer.Ordinal);
        await UserAccountsMigrationsHistoryBootstrap.RunAsync(options, migrations, ct);
        await context.Database.MigrateAsync(ct);
    }

    private static DbContext CreateUserAccountsContext(MigrationRunnerOptions options)
    {
        var dispatcher = new DomainEventDispatcher([]);

        return options.ConfigurationProvider switch
        {
            ConfigurationDatabaseProvider.Sqlite => new UserAccountsSqliteDbContext(
                new DbContextOptionsBuilder<UserAccountsSqliteDbContext>()
                    .UseSqlite(
                        options.ResolvedUserAccountsConnection,
                        sqlite => sqlite.MigrationsHistoryTable(UserAccountsDbContext.MigrationsHistoryTableName))
                    .Options,
                dispatcher),
            ConfigurationDatabaseProvider.PostgreSql => new UserAccountsPostgreSqlDbContext(
                new DbContextOptionsBuilder<UserAccountsPostgreSqlDbContext>()
                    .UseNpgsql(
                        options.ResolvedUserAccountsConnection,
                        npgsql => npgsql.MigrationsHistoryTable(UserAccountsDbContext.MigrationsHistoryTableName))
                    .Options,
                dispatcher),
            _ => throw new InvalidOperationException("Unsupported UserAccounts provider."),
        };
    }
}
