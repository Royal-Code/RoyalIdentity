using Microsoft.EntityFrameworkCore;
using RoyalCode.WorkContext.EntityFramework.Configurations;
using RoyalIdentity.UserAccounts.Infrastructure.Data;
using RoyalIdentity.UserAccounts.PostgreSql;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// PostgreSQL provider wiring for the UserAccounts module persistence.
/// </summary>
public static class UserAccountsPostgreSqlExtensions
{
    /// <summary>
    /// Registers the UserAccounts module backed by a PostgreSQL database whose connection string is read
    /// from configuration by name.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="connectionStringName">The configuration key of the connection string.</param>
    /// <returns>The WorkContext builder for further configuration (e.g. seeding).</returns>
    public static IWorkContextBuilder<UserAccountsPostgreSqlDbContext> AddUserAccountsPostgreSql(
        this IServiceCollection services,
        string connectionStringName = "UserAccounts")
    {
        return services
            .AddPostgreWorkContext<UserAccountsPostgreSqlDbContext>(connectionStringName)
            .ConfigureNpgsqlOptions(options =>
                options.MigrationsHistoryTable(UserAccountsDbContext.MigrationsHistoryTableName))
            .ConfigureUserAccounts();
    }

    /// <summary>
    /// Registers the module with one explicit PostgreSQL connection string supplied by the composition root.
    /// This avoids introducing a second configuration key when the host already owns a typed connection option.
    /// The context deliberately uses the scoped <c>AddDbContext</c> registration rather than pooling: its
    /// constructor consumes the module's scoped domain-event dispatcher.
    /// </summary>
    public static IWorkContextBuilder<UserAccountsPostgreSqlDbContext> AddUserAccountsPostgreSqlConnection(
        this IServiceCollection services,
        string connectionString)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        var builder = services.AddWorkContext<UserAccountsPostgreSqlDbContext>();
        services.AddDbContext<UserAccountsPostgreSqlDbContext>((_, options) => options.UseNpgsql(
            connectionString,
            npgsql => npgsql.MigrationsHistoryTable(UserAccountsDbContext.MigrationsHistoryTableName)));

        return builder.ConfigureUserAccounts();
    }
}
