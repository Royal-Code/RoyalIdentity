namespace RoyalIdentity.Migrations;

public enum ConfigurationDatabaseProvider
{
    Sqlite,
    PostgreSql,
}

/// <summary>
/// Which storage families this run migrates (plan DF23). They evolve independently and keep independent
/// histories, so migrating one never implies the other — even when they share a database.
/// </summary>
[Flags]
public enum StorageFamilySelection
{
    Configuration = 1,
    Operational = 2,
    All = Configuration | Operational,
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
        "[--families <configuration|operational|all>] " +
        "[--operational-connection <value> | --operational-connection-env <name>] " +
        "[--seed <none|product|demo|all>] " +
        "[--server-admin-redirect-uri <absolute-uri> ...] " +
        "[--key-protector <plain|aes|data-protection>] " +
        "[--aes-key-env <name>] " +
        "[--data-protection-key-ring <directory>] " +
        "[--data-protection-app-name <name>]";

    /// <summary>
    /// The provider of both families. One deployment does not mix providers: the families may live in different
    /// databases, but a SQLite Configuration next to a PostgreSQL Operational is not a supported topology.
    /// </summary>
    public required ConfigurationDatabaseProvider ConfigurationProvider { get; init; }

    public required string ConfigurationConnection { get; init; }

    /// <summary>Which families this run migrates. Defaults to Configuration alone.</summary>
    public StorageFamilySelection Families { get; init; } = StorageFamilySelection.Configuration;

    /// <summary>
    /// The Operational connection. <c>null</c> means the families share one database, which is the topology
    /// where the two histories of DF23 actually have to keep them apart.
    /// </summary>
    public string? OperationalConnection { get; init; }

    /// <summary>The connection the Operational family migrates over, sharing the Configuration one by default.</summary>
    public string ResolvedOperationalConnection => OperationalConnection ?? ConfigurationConnection;

    /// <summary>Whether both families were pointed at the same connection string.</summary>
    public bool SharesOneDatabase
        => Families is StorageFamilySelection.All
            && string.Equals(ResolvedOperationalConnection, ConfigurationConnection, StringComparison.Ordinal);

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
        var connection = ResolveConnection(
            values, "--configuration-connection", "--configuration-connection-env", required: true)!;
        var familiesValue = Optional(values, "--families");
        var families = familiesValue is null
            ? StorageFamilySelection.Configuration
            : ParseFamilies(familiesValue);
        var operationalConnection = ResolveConnection(
            values, "--operational-connection", "--operational-connection-env", required: false);

        if (operationalConnection is not null && !families.HasFlag(StorageFamilySelection.Operational))
        {
            throw new MigrationRunnerUsageException(
                "An Operational connection was given but the Operational family was not selected.");
        }

        var seedValue = Optional(values, "--seed");
        var seed = seedValue is null ? ConfigurationSeedMode.None : ParseSeed(seedValue);
        var protectorValue = Optional(values, "--key-protector");
        ConfigurationKeyProtector? protector = protectorValue is null ? null : ParseProtector(protectorValue);

        if (seed is not ConfigurationSeedMode.None && protector is null)
            throw new MigrationRunnerUsageException("--key-protector is required when --seed is enabled.");

        // The seed is Configuration data. Operational holds only what a live protocol flow produces, so there is
        // nothing to seed there and asking for it means the command was misunderstood (plan DF19).
        if (seed is not ConfigurationSeedMode.None && !families.HasFlag(StorageFamilySelection.Configuration))
            throw new MigrationRunnerUsageException("--seed applies to the Configuration family only.");

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
            Families = families,
            OperationalConnection = operationalConnection,
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
            "--families",
            "--operational-connection",
            "--operational-connection-env",
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

    /// <summary>
    /// Resolves a connection from either its direct option or the environment variable that holds it. Giving
    /// both is refused rather than silently preferring one; when <paramref name="required"/> is false, giving
    /// neither means the caller did not ask for that connection.
    /// </summary>
    private static string? ResolveConnection(
        IReadOnlyDictionary<string, List<string>> values,
        string directOption,
        string environmentOption,
        bool required)
    {
        var direct = Optional(values, directOption);
        var environmentName = Optional(values, environmentOption);
        var hasDirect = !string.IsNullOrWhiteSpace(direct);
        var hasEnvironment = !string.IsNullOrWhiteSpace(environmentName);

        if (hasDirect && hasEnvironment)
            throw new MigrationRunnerUsageException($"Specify only one of {directOption} or {environmentOption}.");

        if (!hasDirect && !hasEnvironment)
        {
            return required
                ? throw new MigrationRunnerUsageException(
                    $"Specify exactly one of {directOption} or {environmentOption}.")
                : null;
        }

        if (hasDirect)
            return direct;

        var connection = Environment.GetEnvironmentVariable(environmentName!);
        if (string.IsNullOrWhiteSpace(connection))
            throw new MigrationRunnerUsageException(
                $"Environment variable '{environmentName}' is missing or empty.");
        return connection;
    }

    private static StorageFamilySelection ParseFamilies(string value)
        => value.ToLowerInvariant() switch
        {
            "configuration" => StorageFamilySelection.Configuration,
            "operational" => StorageFamilySelection.Operational,
            "all" or "both" => StorageFamilySelection.All,
            _ => throw new MigrationRunnerUsageException("Unsupported storage family selection."),
        };

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
