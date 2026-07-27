namespace RoyalIdentity.Server.Configuration;

/// <summary>Configuration section names owned by the production Server composition.</summary>
public static class ServerConfigurationSections
{
    /// <summary>Root section for RoyalIdentity host configuration.</summary>
    public const string Root = "RoyalIdentity";

    /// <summary>PostgreSQL connection used by ConfigurationDbContext.</summary>
    public const string ConfigurationConnection = $"{Root}:Connections:Configuration";

    /// <summary>PostgreSQL connection used by OperationalDbContext.</summary>
    public const string OperationalConnection = $"{Root}:Connections:Operational";

    /// <summary>PostgreSQL connection used by UserAccountsDbContext.</summary>
    public const string UserAccountsConnection = $"{Root}:Connections:UserAccounts";

    /// <summary>Configuration snapshot refresh settings.</summary>
    public const string Snapshot = $"{Root}:Snapshot";

    /// <summary>Operational cleanup settings.</summary>
    public const string Cleanup = $"{Root}:Cleanup";

    /// <summary>ASP.NET Core Data Protection settings shared with external provisioning.</summary>
    public const string DataProtection = $"{Root}:DataProtection";

    /// <summary>Global defaults and per-realm overrides for UserAccounts.</summary>
    public const string UserAccountsRealmOptions = $"{Root}:UserAccounts:Options";
}

/// <summary>Base shape for one explicit PostgreSQL DbContext connection.</summary>
public abstract class PostgreSqlConnectionOptions
{
    /// <summary>Gets or sets the complete PostgreSQL connection string for this DbContext.</summary>
    public string ConnectionString { get; set; } = string.Empty;
}

/// <summary>PostgreSQL connection used only by ConfigurationDbContext.</summary>
public sealed class ConfigurationPostgreSqlOptions : PostgreSqlConnectionOptions;

/// <summary>PostgreSQL connection used only by OperationalDbContext.</summary>
public sealed class OperationalPostgreSqlOptions : PostgreSqlConnectionOptions;

/// <summary>PostgreSQL connection used only by UserAccountsDbContext.</summary>
public sealed class UserAccountsPostgreSqlOptions : PostgreSqlConnectionOptions;

/// <summary>
/// ASP.NET Core Data Protection settings shared by the Server and the external provisioning process.
/// Data Protection is the only supported protection mechanism in the production Server composition.
/// </summary>
public sealed class ServerDataProtectionOptions
{
    /// <summary>Gets or sets the persistent key-ring directory, relative to the content root or absolute.</summary>
    public string KeyRingPath { get; set; } = string.Empty;

    /// <summary>Gets or sets the shared Data Protection application discriminator.</summary>
    public string ApplicationName { get; set; } = string.Empty;

    /// <summary>Gets or sets the Operational payload protection profile registered with Data Protection.</summary>
    public string OperationalPayloadProfileId { get; set; } = string.Empty;
}
