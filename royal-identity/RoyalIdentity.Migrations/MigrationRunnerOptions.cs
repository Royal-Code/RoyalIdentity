namespace RoyalIdentity.Migrations;

public enum ConfigurationDatabaseProvider
{
    Sqlite,
    PostgreSql,
}

/// <summary>
/// Whether the selected families share one physical database or live in independent databases. This is
/// deployment topology, not something connection-string equality can prove: one database may legitimately be
/// reached with different credentials or connection options.
/// </summary>
public enum StorageDatabaseTopology
{
    Shared,
    Separate,
}

/// <summary>
/// Which storage families this run migrates (plan DF21/DF23). They evolve independently and keep independent
/// histories, so migrating one never implies the other — even when they share a database.
/// </summary>
[Flags]
public enum StorageFamilySelection
{
    Configuration = 1,
    Operational = 2,
    UserAccounts = 4,
    All = Configuration | Operational | UserAccounts,
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
        "Usage: RoyalIdentity.Migrations --provider <sqlite|postgresql> " +
        "[--families <configuration|operational|user-accounts|comma-separated|all>] " +
        "[--configuration-connection <value> | --configuration-connection-env <name>] " +
        "[--operational-connection <value> | --operational-connection-env <name>] " +
        "[--user-accounts-connection <value> | --user-accounts-connection-env <name>] " +
        "[--database-topology <shared|separate>] " +
        "[--seed <none|product|demo|all>] " +
        "[--server-admin-redirect-uri <absolute-uri> ...] " +
        "[--key-protector <plain|aes|data-protection>] " +
        "[--aes-key-env <name>] " +
        "[--data-protection-key-ring <directory>] " +
        "[--data-protection-app-name <name>]. " +
        "Each selected family requires its own connection. Multiple families default to separate databases; " +
        "specify --database-topology shared when their connections reach the same physical database.";

    /// <summary>
    /// The provider of all selected families. One deployment does not mix providers: the families may live in
    /// different databases, but SQLite and PostgreSQL never participate in the same run.
    /// </summary>
    public required ConfigurationDatabaseProvider ConfigurationProvider { get; init; }

    public string? ConfigurationConnection { get; init; }

    /// <summary>Which families this run migrates. Defaults to Configuration alone.</summary>
    public StorageFamilySelection Families { get; init; } = StorageFamilySelection.Configuration;

    public string? OperationalConnection { get; init; }

    public string? UserAccountsConnection { get; init; }

    public string ResolvedConfigurationConnection
        => RequireSelectedConnection(StorageFamilySelection.Configuration, ConfigurationConnection);

    public string ResolvedOperationalConnection
        => RequireSelectedConnection(StorageFamilySelection.Operational, OperationalConnection);

    public string ResolvedUserAccountsConnection
        => RequireSelectedConnection(StorageFamilySelection.UserAccounts, UserAccountsConnection);

    /// <summary>
    /// Explicit deployment topology. When omitted, multiple selected families are treated as separate databases.
    /// Supplying <see cref="StorageDatabaseTopology.Shared"/> supports one physical database reached through
    /// different connection strings, such as distinct credentials per family.
    /// </summary>
    public StorageDatabaseTopology? DatabaseTopology { get; init; }

    /// <summary>The explicit topology, or the deterministic single/multiple-family default.</summary>
    public StorageDatabaseTopology ResolvedDatabaseTopology
        => DatabaseTopology
            ?? (SelectedFamilyCount <= 1
                ? StorageDatabaseTopology.Shared
                : StorageDatabaseTopology.Separate);

    /// <summary>Whether multiple selected families are declared to live in one physical database.</summary>
    public bool SharesOneDatabase
        => SelectedFamilyCount > 1
            && ResolvedDatabaseTopology is StorageDatabaseTopology.Shared;

    private int SelectedFamilyCount
        => OrderedFamilies.Count(family => Families.HasFlag(family));

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
                ShowHelp = true,
            };
        }

        var values = ParseValues(args);
        var providerValue = Optional(values, "--provider");
        var legacyProviderValue = Optional(values, "--configuration-provider");
        if (providerValue is not null && legacyProviderValue is not null)
        {
            throw new MigrationRunnerUsageException(
                "Specify only one of --provider or --configuration-provider.");
        }
        var provider = ParseProvider(
            providerValue
            ?? legacyProviderValue
            ?? throw new MigrationRunnerUsageException("Option '--provider' is required."));
        var familiesValue = Optional(values, "--families");
        var families = familiesValue is null
            ? StorageFamilySelection.Configuration
            : ParseFamilies(familiesValue);
        var configurationConnection = ResolveConnection(
            values,
            "--configuration-connection",
            "--configuration-connection-env",
            required: families.HasFlag(StorageFamilySelection.Configuration));
        var operationalConnection = ResolveConnection(
            values,
            "--operational-connection",
            "--operational-connection-env",
            required: families.HasFlag(StorageFamilySelection.Operational));
        var userAccountsConnection = ResolveConnection(
            values,
            "--user-accounts-connection",
            "--user-accounts-connection-env",
            required: families.HasFlag(StorageFamilySelection.UserAccounts));
        var topologyValue = Optional(values, "--database-topology");
        StorageDatabaseTopology? databaseTopology = topologyValue is null
            ? null
            : ParseDatabaseTopology(topologyValue);

        if (configurationConnection is not null && !families.HasFlag(StorageFamilySelection.Configuration))
        {
            throw new MigrationRunnerUsageException(
                "A Configuration connection was given but the Configuration family was not selected.");
        }
        if (operationalConnection is not null && !families.HasFlag(StorageFamilySelection.Operational))
        {
            throw new MigrationRunnerUsageException(
                "An Operational connection was given but the Operational family was not selected.");
        }
        if (userAccountsConnection is not null && !families.HasFlag(StorageFamilySelection.UserAccounts))
        {
            throw new MigrationRunnerUsageException(
                "A UserAccounts connection was given but the UserAccounts family was not selected.");
        }
        var seedValue = Optional(values, "--seed");
        var seed = seedValue is null ? ConfigurationSeedMode.None : ParseSeed(seedValue);
        var protectorValue = Optional(values, "--key-protector");
        ConfigurationKeyProtector? protector = protectorValue is null ? null : ParseProtector(protectorValue);

        if (seed is not ConfigurationSeedMode.None && protector is null)
            throw new MigrationRunnerUsageException("--key-protector is required when --seed is enabled.");

        // The seed is Configuration data. Operational rows come from live protocol flows, and UserAccounts is
        // seeded through its own public use cases by the composition that owns that experience.
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

        var options = new MigrationRunnerOptions
        {
            ConfigurationProvider = provider,
            ConfigurationConnection = configurationConnection,
            Families = families,
            OperationalConnection = operationalConnection,
            UserAccountsConnection = userAccountsConnection,
            DatabaseTopology = databaseTopology,
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
        options.ValidateDatabaseTopology();

        return options;
    }

    /// <summary>
    /// Validates the topology independently from command-line parsing because callers may construct these
    /// options directly and invoke <see cref="StorageMigrationRunner"/>.
    /// </summary>
    internal void ValidateDatabaseTopology()
    {
        if (!Enum.IsDefined(ConfigurationProvider))
            throw new MigrationRunnerUsageException("Unsupported storage provider.");

        if (Families is 0 || (Families & ~StorageFamilySelection.All) is not 0)
            throw new MigrationRunnerUsageException("At least one supported storage family must be selected.");

        ValidateConnectionSelection(StorageFamilySelection.Configuration, ConfigurationConnection);
        ValidateConnectionSelection(StorageFamilySelection.Operational, OperationalConnection);
        ValidateConnectionSelection(StorageFamilySelection.UserAccounts, UserAccountsConnection);

        if (DatabaseTopology is not null && SelectedFamilyCount <= 1)
        {
            throw new MigrationRunnerUsageException(
                "--database-topology applies only when multiple storage families are selected.");
        }
    }

    private static Dictionary<string, List<string>> ParseValues(string[] args)
    {
        var known = new HashSet<string>(StringComparer.Ordinal)
        {
            "--provider",
            "--configuration-provider",
            "--configuration-connection",
            "--configuration-connection-env",
            "--families",
            "--operational-connection",
            "--operational-connection-env",
            "--user-accounts-connection",
            "--user-accounts-connection-env",
            "--database-topology",
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
    {
        if (value.Equals("all", StringComparison.OrdinalIgnoreCase))
            return StorageFamilySelection.All;

        var selection = (StorageFamilySelection)0;
        foreach (var item in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            selection |= item.ToLowerInvariant() switch
            {
                "configuration" => StorageFamilySelection.Configuration,
                "operational" => StorageFamilySelection.Operational,
                "user-accounts" or "useraccounts" or "users" => StorageFamilySelection.UserAccounts,
                _ => throw new MigrationRunnerUsageException("Unsupported storage family selection."),
            };
        }

        return selection is 0
            ? throw new MigrationRunnerUsageException("Unsupported storage family selection.")
            : selection;
    }

    private static ConfigurationDatabaseProvider ParseProvider(string value)
        => value.ToLowerInvariant() switch
        {
            "sqlite" => ConfigurationDatabaseProvider.Sqlite,
            "postgresql" or "postgres" => ConfigurationDatabaseProvider.PostgreSql,
            _ => throw new MigrationRunnerUsageException("Unsupported storage provider."),
        };

    private static StorageDatabaseTopology ParseDatabaseTopology(string value)
        => value.ToLowerInvariant() switch
        {
            "shared" => StorageDatabaseTopology.Shared,
            "separate" => StorageDatabaseTopology.Separate,
            _ => throw new MigrationRunnerUsageException("Unsupported database topology."),
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

    private static readonly StorageFamilySelection[] OrderedFamilies =
    [
        StorageFamilySelection.Configuration,
        StorageFamilySelection.Operational,
        StorageFamilySelection.UserAccounts,
    ];

    private void ValidateConnectionSelection(StorageFamilySelection family, string? connection)
    {
        if (Families.HasFlag(family) && string.IsNullOrWhiteSpace(connection))
        {
            throw new MigrationRunnerUsageException(
                $"The {family} family requires its own connection string.");
        }
        if (!Families.HasFlag(family) && !string.IsNullOrWhiteSpace(connection))
        {
            throw new MigrationRunnerUsageException(
                $"A {family} connection was given but the family was not selected.");
        }
    }

    private string RequireSelectedConnection(StorageFamilySelection family, string? connection)
    {
        if (!Families.HasFlag(family) || string.IsNullOrWhiteSpace(connection))
        {
            throw new MigrationRunnerUsageException(
                $"The {family} family does not have a selected connection string.");
        }

        return connection;
    }
}

public sealed class MigrationRunnerUsageException(string message) : Exception(message);
