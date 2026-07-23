namespace Tests.Storage.Configuration;

internal static class ConfigurationPostgreSqlTestEnvironment
{
	public const string ConnectionStringVariable = "ROYALIDENTITY_CONFIGURATION_TEST_POSTGRES";

	public static string ConnectionString =>
		Environment.GetEnvironmentVariable(ConnectionStringVariable)
		?? throw new InvalidOperationException(
			$"Environment variable {ConnectionStringVariable} is required for PostgreSQL tests.");
}

[AttributeUsage(AttributeTargets.Method)]
internal sealed class ConfigurationPostgreSqlFactAttribute : FactAttribute
{
	public ConfigurationPostgreSqlFactAttribute()
	{
		if (string.IsNullOrWhiteSpace(
			Environment.GetEnvironmentVariable(ConfigurationPostgreSqlTestEnvironment.ConnectionStringVariable)))
		{
			Skip = $"Set {ConfigurationPostgreSqlTestEnvironment.ConnectionStringVariable} or run " +
				"scripts/Test-ConfigurationPostgreSql.ps1 to execute PostgreSQL tests.";
		}
	}
}
