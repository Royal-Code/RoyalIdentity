using Microsoft.EntityFrameworkCore;
using RoyalCode.WorkContext.EntityFramework.Configurations;
using RoyalIdentity.UserAccounts.Infrastructure.Data;
using RoyalIdentity.UserAccounts.Sqlite;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// SQLite provider wiring for the UserAccounts module persistence.
/// </summary>
public static class UserAccountsSqliteExtensions
{
    /// <summary>
    /// Registers the UserAccounts module backed by a SQLite database whose connection string is read
    /// from configuration by name.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="connectionStringName">The configuration key of the connection string.</param>
    /// <returns>The WorkContext builder for further configuration (e.g. seeding).</returns>
    public static IWorkContextBuilder<UserAccountsSqliteDbContext> AddUserAccountsSqlite(
        this IServiceCollection services,
        string connectionStringName = "UserAccounts")
    {
        return services
            .AddSqliteWorkContext<UserAccountsSqliteDbContext>(connectionStringName)
            .ConfigureSqliteOptions(options =>
                options.MigrationsHistoryTable(UserAccountsDbContext.MigrationsHistoryTableName))
            .ConfigureUserAccounts();
    }

    /// <summary>
    /// Registers the module with one explicit SQLite connection string supplied by the composition root.
    /// The caller owns any keep-alive connection required by a named in-memory database.
    /// </summary>
    public static IWorkContextBuilder<UserAccountsSqliteDbContext> AddUserAccountsSqliteConnection(
        this IServiceCollection services,
        string connectionString)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        var builder = services.AddWorkContext<UserAccountsSqliteDbContext>();
        services.AddDbContext<UserAccountsSqliteDbContext>((_, options) => options.UseSqlite(
            connectionString,
            sqlite => sqlite.MigrationsHistoryTable(UserAccountsDbContext.MigrationsHistoryTableName)));

        return builder.ConfigureUserAccounts();
    }

    /// <summary>
    /// Registers the UserAccounts module backed by an in-memory SQLite connection and creates the schema.
    /// Intended for tests and ephemeral scenarios.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The WorkContext builder for further configuration (e.g. seeding).</returns>
    public static IWorkContextBuilder<UserAccountsSqliteDbContext> AddUserAccountsSqliteInMemory(
        this IServiceCollection services)
    {
        return services
            .AddSqliteInMemoryWorkContext<UserAccountsSqliteDbContext>()
            .ConfigureSqliteOptions(options =>
                options.MigrationsHistoryTable(UserAccountsDbContext.MigrationsHistoryTableName))
            .ConfigureUserAccounts()
            .EnsureDatabaseCreated();
    }
}
