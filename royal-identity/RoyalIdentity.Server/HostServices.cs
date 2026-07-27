using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using RoyalIdentity.Configuration;
using RoyalIdentity.Extensions;
using RoyalIdentity.Server.Configuration;
using RoyalIdentity.Storage.EntityFramework.Extensions;
using RoyalIdentity.Storage.EntityFramework.Operational.Maintenance;
using RoyalIdentity.Storage.EntityFramework.PostgreSql;

namespace RoyalIdentity.Server;

public static class HostServices
{
    public static IServiceCollection AddHostServices(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);

        services.AddRoyalIdentityServerConfiguration(configuration, environment);
        services.AddRoyalIdentityServerPersistence(configuration, environment);
        services.AddRoyalIdentityRazor();
        services.AddOpenIdConnectProviderServices();
        services.AddOperationalPayloadProfileStartupValidation();

        return services;
    }

    /// <summary>
    /// Composes the production storage graph. It only registers PostgreSQL-backed services and never connects,
    /// inspects schema state, applies migrations or seeds data.
    /// </summary>
    public static IServiceCollection AddRoyalIdentityServerPersistence(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);

        var configurationConnection = RequiredConfiguredConnection(
            configuration, ServerConfigurationSections.ConfigurationConnection);
        var operationalConnection = RequiredConfiguredConnection(
            configuration, ServerConfigurationSections.OperationalConnection);
        var userAccountsConnection = RequiredConfiguredConnection(
            configuration, ServerConfigurationSections.UserAccountsConnection);
        var dataProtection = Bind<ServerDataProtectionOptions>(
            configuration, ServerConfigurationSections.DataProtection);
        var cleanup = Bind<OperationalCleanupOptions>(
            configuration, ServerConfigurationSections.Cleanup);

        var dataProtectionBuilder = services.AddDataProtection();
        if (!string.IsNullOrWhiteSpace(dataProtection.ApplicationName))
            dataProtectionBuilder.SetApplicationName(dataProtection.ApplicationName);
        if (!string.IsNullOrWhiteSpace(dataProtection.KeyRingPath))
        {
            var keyRingPath = Path.IsPathFullyQualified(dataProtection.KeyRingPath)
                ? Path.GetFullPath(dataProtection.KeyRingPath)
                : Path.GetFullPath(dataProtection.KeyRingPath, environment.ContentRootPath);
            dataProtectionBuilder.PersistKeysToFileSystem(new DirectoryInfo(keyRingPath));
        }

        services.AddDbContext<ConfigurationPostgreSqlDbContext>(options => options.UseNpgsql(
            configurationConnection,
            npgsql => npgsql.UseConfigurationMigrationsHistory()));
        services.AddDbContext<OperationalPostgreSqlDbContext>(options => options.UseNpgsql(
            operationalConnection,
            npgsql => npgsql.UseOperationalMigrationsHistory()));

        services.AddEntityFrameworkConfigurationStorage<ConfigurationPostgreSqlDbContext>();
        services.AddEntityFrameworkConfigurationSnapshotSource();
        services.AddSingleton(provider =>
        {
            var configured = provider
                .GetRequiredService<IOptions<ConfigurationSnapshotRefreshOptions>>()
                .Value;
            return new ConfigurationSnapshotRefreshOptions
            {
                RefreshInterval = configured.RefreshInterval,
            };
        });

        services.AddEntityFrameworkOperationalStorage<OperationalPostgreSqlDbContext>();
        if (!string.IsNullOrWhiteSpace(dataProtection.OperationalPayloadProfileId))
        {
            services.AddOperationalDataProtectionPayloadProtection(
                dataProtection.OperationalPayloadProfileId);
        }
        services.AddEntityFrameworkOperationalCleanup(options => Copy(cleanup, options));
        services.AddAspNetDataProtectionKeyMaterialProtector();
        services.AddEntityFrameworkStorage();

        services.AddUserAccountsPostgreSqlConnection(userAccountsConnection);
        services.AddUserAccountsForRoyalIdentity();

        return services;
    }

    private static string RequiredConfiguredConnection(IConfiguration configuration, string sectionName)
    {
        var configured = Bind<PostgreSqlConnectionValue>(configuration, sectionName);
        if (string.IsNullOrWhiteSpace(configured.ConnectionString))
            throw new InvalidOperationException($"{sectionName}:ConnectionString is required.");

        return configured.ConnectionString;
    }

    private static TOptions Bind<TOptions>(IConfiguration configuration, string sectionName)
        where TOptions : class, new()
    {
        var options = new TOptions();
        configuration.GetSection(sectionName).Bind(options);
        return options;
    }

    private static void Copy(OperationalCleanupOptions source, OperationalCleanupOptions target)
    {
        target.Mode = source.Mode;
        target.Interval = source.Interval;
        target.BatchSize = source.BatchSize;
        target.MaxRefreshTokenPostConsumedTolerance = source.MaxRefreshTokenPostConsumedTolerance;
    }

    private sealed class PostgreSqlConnectionValue
    {
        public string ConnectionString { get; set; } = string.Empty;
    }
}
