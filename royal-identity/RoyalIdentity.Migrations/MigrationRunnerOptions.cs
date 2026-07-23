namespace RoyalIdentity.Migrations;

public enum ConfigurationDatabaseProvider
{
	Sqlite,
	PostgreSql,
}

[Flags]
public enum ConfigurationSeedMode
{
	None = 0,
	Product = 1,
	Demo = 2,
	All = Product | Demo,
}

public enum ConfigurationKeyProtector
{
	Plain,
	Aes,
	DataProtection,
}

public sealed class MigrationRunnerOptions
{
	public const string Usage =
		"Usage: RoyalIdentity.Migrations --configuration-provider <sqlite|postgresql> " +
		"(--configuration-connection <value> | --configuration-connection-env <name>) " +
		"[--seed <none|product|demo|all>] " +
		"[--key-protector <plain|aes|data-protection>] " +
		"[--aes-key-env <name>] " +
		"[--data-protection-key-ring <directory>] " +
		"[--data-protection-app-name <name>]";

	public required ConfigurationDatabaseProvider ConfigurationProvider { get; init; }

	public required string ConfigurationConnection { get; init; }

	public ConfigurationSeedMode Seed { get; init; }

	public ConfigurationKeyProtector? KeyProtector { get; init; }

	public string? AesKeyEnvironmentVariable { get; init; }

	public string? DataProtectionKeyRing { get; init; }

	public string DataProtectionApplicationName { get; init; } = "RoyalIdentity.Configuration";

	public bool ShowHelp { get; init; }

	public static MigrationRunnerOptions Parse(string[] args)
	{
		ArgumentNullException.ThrowIfNull(args);
		if (args.Any(argument => argument is "--help" or "-h"))
		{
			return new MigrationRunnerOptions
			{
				ConfigurationProvider = ConfigurationDatabaseProvider.Sqlite,
				ConfigurationConnection = string.Empty,
				ShowHelp = true,
			};
		}

		var values = ParsePairs(args);
		var provider = ParseProvider(Required(values, "--configuration-provider"));
		var connection = ResolveConnection(values);
		var seed = values.TryGetValue("--seed", out var seedValue)
			? ParseSeed(seedValue)
			: ConfigurationSeedMode.None;
		ConfigurationKeyProtector? protector = values.TryGetValue("--key-protector", out var protectorValue)
			? ParseProtector(protectorValue)
			: null;

		if (seed is not ConfigurationSeedMode.None && protector is null)
			throw new MigrationRunnerUsageException("--key-protector is required when --seed is enabled.");

		var aesKeyEnvironmentVariable = Optional(values, "--aes-key-env");
		var dataProtectionKeyRing = Optional(values, "--data-protection-key-ring");
		var dataProtectionApplicationName = Optional(values, "--data-protection-app-name")
			?? "RoyalIdentity.Configuration";

		if (protector is ConfigurationKeyProtector.Aes && string.IsNullOrWhiteSpace(aesKeyEnvironmentVariable))
			throw new MigrationRunnerUsageException("--aes-key-env is required for the AES protector.");
		if (protector is ConfigurationKeyProtector.DataProtection && string.IsNullOrWhiteSpace(dataProtectionKeyRing))
			throw new MigrationRunnerUsageException(
				"--data-protection-key-ring is required for the Data Protection protector.");

		return new MigrationRunnerOptions
		{
			ConfigurationProvider = provider,
			ConfigurationConnection = connection,
			Seed = seed,
			KeyProtector = protector,
			AesKeyEnvironmentVariable = aesKeyEnvironmentVariable,
			DataProtectionKeyRing = dataProtectionKeyRing,
			DataProtectionApplicationName = dataProtectionApplicationName,
		};
	}

	private static Dictionary<string, string> ParsePairs(string[] args)
	{
		var known = new HashSet<string>(StringComparer.Ordinal)
		{
			"--configuration-provider",
			"--configuration-connection",
			"--configuration-connection-env",
			"--seed",
			"--key-protector",
			"--aes-key-env",
			"--data-protection-key-ring",
			"--data-protection-app-name",
		};
		var values = new Dictionary<string, string>(StringComparer.Ordinal);

		for (var index = 0; index < args.Length; index += 2)
		{
			var name = args[index];
			if (!known.Contains(name))
				throw new MigrationRunnerUsageException($"Unknown option '{name}'.");
			if (index + 1 >= args.Length || args[index + 1].StartsWith("--", StringComparison.Ordinal))
				throw new MigrationRunnerUsageException($"Option '{name}' requires a value.");
			if (!values.TryAdd(name, args[index + 1]))
				throw new MigrationRunnerUsageException($"Option '{name}' was specified more than once.");
		}

		return values;
	}

	private static string ResolveConnection(IReadOnlyDictionary<string, string> values)
	{
		var direct = Optional(values, "--configuration-connection");
		var environmentName = Optional(values, "--configuration-connection-env");
		if (string.IsNullOrWhiteSpace(direct) == string.IsNullOrWhiteSpace(environmentName))
		{
			throw new MigrationRunnerUsageException(
				"Specify exactly one of --configuration-connection or --configuration-connection-env.");
		}

		if (!string.IsNullOrWhiteSpace(direct))
			return direct;

		var connection = Environment.GetEnvironmentVariable(environmentName!);
		if (string.IsNullOrWhiteSpace(connection))
			throw new MigrationRunnerUsageException(
				$"Environment variable '{environmentName}' is missing or empty.");
		return connection;
	}

	private static ConfigurationDatabaseProvider ParseProvider(string value)
		=> value.ToLowerInvariant() switch
		{
			"sqlite" => ConfigurationDatabaseProvider.Sqlite,
			"postgresql" or "postgres" => ConfigurationDatabaseProvider.PostgreSql,
			_ => throw new MigrationRunnerUsageException("Unsupported Configuration provider."),
		};

	private static ConfigurationSeedMode ParseSeed(string value)
		=> value.ToLowerInvariant() switch
		{
			"none" => ConfigurationSeedMode.None,
			"product" => ConfigurationSeedMode.Product,
			"demo" => ConfigurationSeedMode.Demo,
			"all" => ConfigurationSeedMode.All,
			_ => throw new MigrationRunnerUsageException("Unsupported seed mode."),
		};

	private static ConfigurationKeyProtector ParseProtector(string value)
		=> value.ToLowerInvariant() switch
		{
			"plain" => ConfigurationKeyProtector.Plain,
			"aes" or "aes-gcm" => ConfigurationKeyProtector.Aes,
			"data-protection" or "aspnet-data-protection" => ConfigurationKeyProtector.DataProtection,
			_ => throw new MigrationRunnerUsageException("Unsupported key protector."),
		};

	private static string Required(IReadOnlyDictionary<string, string> values, string name)
		=> Optional(values, name)
			?? throw new MigrationRunnerUsageException($"Option '{name}' is required.");

	private static string? Optional(IReadOnlyDictionary<string, string> values, string name)
		=> values.TryGetValue(name, out var value) && !string.IsNullOrWhiteSpace(value) ? value : null;
}

public sealed class MigrationRunnerUsageException(string message) : Exception(message);
