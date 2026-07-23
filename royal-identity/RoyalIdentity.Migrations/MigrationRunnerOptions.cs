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
		"[--server-admin-redirect-uri <absolute-uri> ...] " +
		"[--key-protector <plain|aes|data-protection>] " +
		"[--aes-key-env <name>] " +
		"[--data-protection-key-ring <directory>] " +
		"[--data-protection-app-name <name>]";

	public required ConfigurationDatabaseProvider ConfigurationProvider { get; init; }

	public required string ConfigurationConnection { get; init; }

	public ConfigurationSeedMode Seed { get; init; }

	public ConfigurationKeyProtector? KeyProtector { get; init; }

	public ConfigurationProductSeedOptions ProductSeed { get; init; } = new();

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

		var values = ParseValues(args);
		var provider = ParseProvider(Required(values, "--configuration-provider"));
		var connection = ResolveConnection(values);
		var seedValue = Optional(values, "--seed");
		var seed = seedValue is null ? ConfigurationSeedMode.None : ParseSeed(seedValue);
		var protectorValue = Optional(values, "--key-protector");
		ConfigurationKeyProtector? protector = protectorValue is null ? null : ParseProtector(protectorValue);

		if (seed is not ConfigurationSeedMode.None && protector is null)
			throw new MigrationRunnerUsageException("--key-protector is required when --seed is enabled.");

		var aesKeyEnvironmentVariable = Optional(values, "--aes-key-env");
		var dataProtectionKeyRing = Optional(values, "--data-protection-key-ring");
		var dataProtectionApplicationName = Optional(values, "--data-protection-app-name")
			?? "RoyalIdentity.Configuration";
		var serverAdminRedirectUris = Many(values, "--server-admin-redirect-uri");

		if (seed.HasFlag(ConfigurationSeedMode.Product) && serverAdminRedirectUris.Count is 0)
		{
			throw new MigrationRunnerUsageException(
				"--server-admin-redirect-uri is required at least once for the product seed.");
		}
		if (!seed.HasFlag(ConfigurationSeedMode.Product) && serverAdminRedirectUris.Count is not 0)
		{
			throw new MigrationRunnerUsageException(
				"--server-admin-redirect-uri can only be used with the product or all seed.");
		}
		ValidateServerAdminRedirectUris(serverAdminRedirectUris);

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
			ProductSeed = new ConfigurationProductSeedOptions
			{
				ServerAdminRedirectUris = serverAdminRedirectUris,
			},
			AesKeyEnvironmentVariable = aesKeyEnvironmentVariable,
			DataProtectionKeyRing = dataProtectionKeyRing,
			DataProtectionApplicationName = dataProtectionApplicationName,
		};
	}

	private static Dictionary<string, List<string>> ParseValues(string[] args)
	{
		var known = new HashSet<string>(StringComparer.Ordinal)
		{
			"--configuration-provider",
			"--configuration-connection",
			"--configuration-connection-env",
			"--seed",
			"--server-admin-redirect-uri",
			"--key-protector",
			"--aes-key-env",
			"--data-protection-key-ring",
			"--data-protection-app-name",
		};
		var repeatable = new HashSet<string>(StringComparer.Ordinal)
		{
			"--server-admin-redirect-uri",
		};
		var values = new Dictionary<string, List<string>>(StringComparer.Ordinal);

		for (var index = 0; index < args.Length; index += 2)
		{
			var name = args[index];
			if (!known.Contains(name))
				throw new MigrationRunnerUsageException($"Unknown option '{name}'.");
			if (index + 1 >= args.Length || args[index + 1].StartsWith("--", StringComparison.Ordinal))
				throw new MigrationRunnerUsageException($"Option '{name}' requires a value.");
			if (!values.TryGetValue(name, out var optionValues))
			{
				optionValues = [];
				values.Add(name, optionValues);
			}
			else if (!repeatable.Contains(name))
			{
				throw new MigrationRunnerUsageException($"Option '{name}' was specified more than once.");
			}

			optionValues.Add(args[index + 1]);
		}

		return values;
	}

	private static string ResolveConnection(IReadOnlyDictionary<string, List<string>> values)
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

	private static string Required(IReadOnlyDictionary<string, List<string>> values, string name)
		=> Optional(values, name)
			?? throw new MigrationRunnerUsageException($"Option '{name}' is required.");

	private static string? Optional(IReadOnlyDictionary<string, List<string>> values, string name)
		=> values.TryGetValue(name, out var optionValues)
			&& optionValues.Count is 1
			&& !string.IsNullOrWhiteSpace(optionValues[0])
				? optionValues[0]
				: null;

	private static IReadOnlyList<string> Many(
		IReadOnlyDictionary<string, List<string>> values,
		string name)
		=> values.TryGetValue(name, out var optionValues) ? optionValues.ToArray() : [];

	private static void ValidateServerAdminRedirectUris(IReadOnlyList<string> redirectUris)
	{
		var unique = new HashSet<string>(StringComparer.Ordinal);
		foreach (var redirectUri in redirectUris)
		{
			if (!Uri.TryCreate(redirectUri, UriKind.Absolute, out _))
			{
				throw new MigrationRunnerUsageException(
					"--server-admin-redirect-uri must contain an absolute URI.");
			}
			if (!unique.Add(redirectUri))
			{
				throw new MigrationRunnerUsageException(
					"--server-admin-redirect-uri cannot contain duplicate values.");
			}
		}
	}
}

public sealed class MigrationRunnerUsageException(string message) : Exception(message);
