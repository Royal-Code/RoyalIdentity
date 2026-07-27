using Microsoft.Extensions.Options;
using Npgsql;
using RoyalIdentity.Configuration;
using RoyalIdentity.Storage.EntityFramework.Operational.Maintenance;

namespace RoyalIdentity.Server.Configuration;

/// <summary>Registers and validates configuration owned by the production Server composition.</summary>
public static class ServerConfigurationServiceCollectionExtensions
{
    /// <summary>
    /// Binds the fixed PostgreSQL Server configuration. Validation runs during host startup and never includes
    /// connection-string values in failure messages.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The host configuration.</param>
    /// <param name="environment">The host environment used to validate relative Data Protection paths.</param>
    /// <returns>The same service collection for chaining.</returns>
    public static IServiceCollection AddRoyalIdentityServerConfiguration(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);

        AddPostgreSqlConnection<ConfigurationPostgreSqlOptions>(
            services, configuration, ServerConfigurationSections.ConfigurationConnection);
        AddPostgreSqlConnection<OperationalPostgreSqlOptions>(
            services, configuration, ServerConfigurationSections.OperationalConnection);
        AddPostgreSqlConnection<UserAccountsPostgreSqlOptions>(
            services, configuration, ServerConfigurationSections.UserAccountsConnection);

        services.AddOptions<ConfigurationSnapshotRefreshOptions>()
            .Bind(configuration.GetSection(ServerConfigurationSections.Snapshot))
            .Validate(
                static options => options.RefreshInterval > TimeSpan.Zero,
                $"{ServerConfigurationSections.Snapshot}:RefreshInterval must be a positive duration.")
            .ValidateOnStart();

        services.AddOptions<OperationalCleanupOptions>()
            .Bind(configuration.GetSection(ServerConfigurationSections.Cleanup))
            .Validate(
                static options => Enum.IsDefined(options.Mode) &&
                    options.Mode is not CleanupExecutionMode.Unspecified,
                $"{ServerConfigurationSections.Cleanup}:Mode must be selected explicitly as Hosted or External.")
            .Validate(
                static options => options.Mode is not CleanupExecutionMode.Hosted ||
                    options.Interval > TimeSpan.Zero,
                $"{ServerConfigurationSections.Cleanup}:Interval must be positive in Hosted mode.")
            .Validate(
                static options => options.BatchSize > 0,
                $"{ServerConfigurationSections.Cleanup}:BatchSize must be positive.")
            .Validate(
                static options => options.MaxRefreshTokenPostConsumedTolerance is not { } tolerance ||
                    tolerance >= TimeSpan.Zero,
                $"{ServerConfigurationSections.Cleanup}:MaxRefreshTokenPostConsumedTolerance cannot be negative.")
            .ValidateOnStart();

        var dataProtectionSection = configuration.GetSection(ServerConfigurationSections.DataProtection);
        services.AddOptions<ServerDataProtectionOptions>()
            .Bind(dataProtectionSection)
            .Validate(
                static options => !string.IsNullOrWhiteSpace(options.ApplicationName),
                $"{ServerConfigurationSections.DataProtection}:ApplicationName is required.")
            .Validate(
                options => IsValidKeyRingPath(options.KeyRingPath, environment.ContentRootPath),
                $"{ServerConfigurationSections.DataProtection}:KeyRingPath must be a valid path.")
            .Validate(
                static options => !string.IsNullOrWhiteSpace(options.OperationalPayloadProfileId),
                $"{ServerConfigurationSections.DataProtection}:OperationalPayloadProfileId is required.")
            .Validate(
                _ => string.IsNullOrWhiteSpace(dataProtectionSection["Provider"]),
                "The Server does not support a protection provider selector; ASP.NET Core Data Protection is required.")
            .ValidateOnStart();

        services.AddConfiguredUserAccountsRealmOptions(
            configuration.GetSection(ServerConfigurationSections.UserAccountsRealmOptions));

        return services;
    }

    private static void AddPostgreSqlConnection<TOptions>(
        IServiceCollection services,
        IConfiguration configuration,
        string sectionName)
        where TOptions : PostgreSqlConnectionOptions
    {
        services.AddOptions<TOptions>()
            .Bind(configuration.GetSection(sectionName))
            .Validate(
                static options => IsValidPostgreSqlConnectionString(options.ConnectionString),
                $"{sectionName}:ConnectionString must be a valid PostgreSQL connection string.")
            .ValidateOnStart();
    }

    private static bool IsValidPostgreSqlConnectionString(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            return false;

        try
        {
            var builder = new NpgsqlConnectionStringBuilder(connectionString);
            return !string.IsNullOrWhiteSpace(builder.Host) &&
                !string.IsNullOrWhiteSpace(builder.Database);
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static bool IsValidKeyRingPath(string? configuredPath, string contentRootPath)
    {
        if (string.IsNullOrWhiteSpace(configuredPath) || string.IsNullOrWhiteSpace(contentRootPath))
            return false;

        try
        {
            _ = Path.IsPathFullyQualified(configuredPath)
                ? Path.GetFullPath(configuredPath)
                : Path.GetFullPath(configuredPath, contentRootPath);
            return true;
        }
        catch (Exception exception) when (
            exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }
    }
}
