namespace Tests.Storage.Configuration;

/// <summary>
/// The opt-in PostgreSQL server the storage suites run against. One server serves both families —
/// Configuration since Plano 2, Operational since plan-data-operational-storage Fase 7 — because the
/// interesting topology is exactly the shared one: two families, two migrations histories, one database
/// (plan DF23). The variable keeps its original name so existing scripts and local setups keep working.
/// </summary>
internal static class StoragePostgreSqlTestEnvironment
{
    public const string ConnectionStringVariable = "ROYALIDENTITY_CONFIGURATION_TEST_POSTGRES";

    public static string ConnectionString =>
        Environment.GetEnvironmentVariable(ConnectionStringVariable)
        ?? throw new InvalidOperationException(
            $"Environment variable {ConnectionStringVariable} is required for PostgreSQL tests.");
}

/// <summary>Skips the scenario unless the opt-in PostgreSQL server is configured.</summary>
[AttributeUsage(AttributeTargets.Method)]
internal sealed class StoragePostgreSqlFactAttribute : FactAttribute
{
    public StoragePostgreSqlFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(
            Environment.GetEnvironmentVariable(StoragePostgreSqlTestEnvironment.ConnectionStringVariable)))
        {
            Skip = $"Set {StoragePostgreSqlTestEnvironment.ConnectionStringVariable} or run " +
                "scripts/Test-ConfigurationPostgreSql.ps1 / scripts/Test-OperationalPostgreSql.ps1 to execute " +
                "PostgreSQL tests.";
        }
    }
}
