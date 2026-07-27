extern alias RoyalIdentityServer;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using RoyalIdentity.Configuration;
using RoyalIdentityServer::RoyalIdentity.Server.Configuration;
using RoyalIdentity.Storage.EntityFramework.Operational.Maintenance;
using RoyalIdentity.UserAccounts.Integration;

namespace Tests.Integration.Configuration;

public class HostConfigurationTests
{
    private const string SharedConnection =
        "Host=localhost;Database=royal_identity;Username=royal_identity;Password=local-only";

    [Fact]
    public void ThreeExplicitConnections_MayPointToTheSamePostgreSqlDatabase()
    {
        using var provider = BuildProvider(ValidConfiguration());

        var configuration = provider.GetRequiredService<IOptions<ConfigurationPostgreSqlOptions>>().Value;
        var operational = provider.GetRequiredService<IOptions<OperationalPostgreSqlOptions>>().Value;
        var userAccounts = provider.GetRequiredService<IOptions<UserAccountsPostgreSqlOptions>>().Value;

        Assert.Equal(SharedConnection, configuration.ConnectionString);
        Assert.Equal(SharedConnection, operational.ConnectionString);
        Assert.Equal(SharedConnection, userAccounts.ConnectionString);
    }

    [Theory]
    [InlineData("Configuration")]
    [InlineData("Operational")]
    [InlineData("UserAccounts")]
    public void MissingConnection_IsRejectedWithoutDisclosingOtherConnectionValues(string family)
    {
        var values = ValidConfiguration();
        values[$"{ServerConfigurationSections.Root}:Connections:{family}:ConnectionString"] = null;
        using var provider = BuildProvider(values);

        var exception = Assert.Throws<OptionsValidationException>(() => ResolveConnection(provider, family));

        Assert.DoesNotContain("local-only", exception.Message, StringComparison.Ordinal);
        Assert.Contains($"{family}:ConnectionString", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void MalformedConnection_IsRejectedWithoutDisclosingTheSecret()
    {
        const string secret = "must-never-appear";
        var values = ValidConfiguration();
        values[$"{ServerConfigurationSections.ConfigurationConnection}:ConnectionString"] =
            $"Host=localhost;Database=royal_identity;Password={secret};Broken";
        using var provider = BuildProvider(values);

        var exception = Assert.Throws<OptionsValidationException>(
            () => provider.GetRequiredService<IOptions<ConfigurationPostgreSqlOptions>>().Value);

        Assert.DoesNotContain(secret, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void SnapshotWithoutPositiveInterval_IsRejected()
    {
        var values = ValidConfiguration();
        values[$"{ServerConfigurationSections.Snapshot}:RefreshInterval"] = "00:00:00";
        using var provider = BuildProvider(values);

        Assert.Throws<OptionsValidationException>(
            () => provider.GetRequiredService<IOptions<ConfigurationSnapshotRefreshOptions>>().Value);
    }

    [Fact]
    public void CleanupWithoutExplicitMode_IsRejected()
    {
        var values = ValidConfiguration();
        values[$"{ServerConfigurationSections.Cleanup}:Mode"] = null;
        using var provider = BuildProvider(values);

        Assert.Throws<OptionsValidationException>(
            () => provider.GetRequiredService<IOptions<OperationalCleanupOptions>>().Value);
    }

    [Fact]
    public void ProtectionProviderSelector_IsRejectedBecauseDataProtectionIsMandatory()
    {
        var values = ValidConfiguration();
        values[$"{ServerConfigurationSections.DataProtection}:Provider"] = "Aes";
        using var provider = BuildProvider(values);

        var exception = Assert.Throws<OptionsValidationException>(
            () => provider.GetRequiredService<IOptions<ServerDataProtectionOptions>>().Value);

        Assert.Contains("Data Protection is required", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void MissingDataProtectionConfiguration_IsRejected()
    {
        var values = ValidConfiguration();
        values[$"{ServerConfigurationSections.DataProtection}:KeyRingPath"] = null;
        values[$"{ServerConfigurationSections.DataProtection}:ApplicationName"] = null;
        values[$"{ServerConfigurationSections.DataProtection}:OperationalPayloadProfileId"] = null;
        using var provider = BuildProvider(values);

        Assert.Throws<OptionsValidationException>(
            () => provider.GetRequiredService<IOptions<ServerDataProtectionOptions>>().Value);
    }

    [Fact]
    public void UserAccountsOptions_ApplyDefaultsAndIndependentRealmOverrides()
    {
        var values = ValidConfiguration();
        var section = ServerConfigurationSections.UserAccountsRealmOptions;
        values[$"{section}:Defaults:AllowRegistration"] = "false";
        values[$"{section}:Defaults:PasswordOptions:MinimumLength"] = "8";
        values[$"{section}:Realms:realm-a:AllowRegistration"] = "true";
        values[$"{section}:Realms:realm-b:PasswordOptions:MinimumLength"] = "12";
        using var provider = BuildProvider(values);

        _ = provider.GetRequiredService<IOptions<UserAccountsRealmOptionsConfiguration>>().Value;
        var resolver = provider.GetRequiredService<IUserAccountsRealmOptionsResolver>();

        var realmA = resolver.Resolve("realm-a");
        var realmB = resolver.Resolve("realm-b");
        var realmWithoutOverride = resolver.Resolve("realm-c");

        Assert.True(realmA.AllowRegistration);
        Assert.Equal(8, realmA.PasswordOptions.MinimumLength);
        Assert.False(realmB.AllowRegistration);
        Assert.Equal(12, realmB.PasswordOptions.MinimumLength);
        Assert.False(realmWithoutOverride.AllowRegistration);
        Assert.Equal(8, realmWithoutOverride.PasswordOptions.MinimumLength);
        Assert.NotSame(realmA, resolver.Resolve("realm-a"));
    }

    [Fact]
    public void InvalidUserAccountsDefaults_AreRejectedDuringOptionsValidation()
    {
        var values = ValidConfiguration();
        var section = ServerConfigurationSections.UserAccountsRealmOptions;
        values[$"{section}:Defaults:EmailAsUsername"] = "true";
        values[$"{section}:Defaults:AllowDuplicateEmail"] = "true";
        using var provider = BuildProvider(values);

        Assert.Throws<OptionsValidationException>(
            () => provider.GetRequiredService<IOptions<UserAccountsRealmOptionsConfiguration>>().Value);
    }

    private static ServiceProvider BuildProvider(Dictionary<string, string?> values)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
        var services = new ServiceCollection();
        services.AddRoyalIdentityServerConfiguration(configuration, new TestHostEnvironment());
        return services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });
    }

    private static Dictionary<string, string?> ValidConfiguration()
    {
        return new Dictionary<string, string?>
        {
            [$"{ServerConfigurationSections.ConfigurationConnection}:ConnectionString"] = SharedConnection,
            [$"{ServerConfigurationSections.OperationalConnection}:ConnectionString"] = SharedConnection,
            [$"{ServerConfigurationSections.UserAccountsConnection}:ConnectionString"] = SharedConnection,
            [$"{ServerConfigurationSections.Snapshot}:RefreshInterval"] = "00:05:00",
            [$"{ServerConfigurationSections.Cleanup}:Mode"] = "External",
            [$"{ServerConfigurationSections.Cleanup}:Interval"] = "00:15:00",
            [$"{ServerConfigurationSections.Cleanup}:BatchSize"] = "500",
            [$"{ServerConfigurationSections.DataProtection}:KeyRingPath"] = "data-protection-keys",
            [$"{ServerConfigurationSections.DataProtection}:ApplicationName"] = "RoyalIdentity.Server",
            [$"{ServerConfigurationSections.DataProtection}:OperationalPayloadProfileId"] = "data-protection",
        };
    }

    private static void ResolveConnection(IServiceProvider provider, string family)
    {
        _ = family switch
        {
            "Configuration" => provider
                .GetRequiredService<IOptions<ConfigurationPostgreSqlOptions>>().Value.ConnectionString,
            "Operational" => provider
                .GetRequiredService<IOptions<OperationalPostgreSqlOptions>>().Value.ConnectionString,
            "UserAccounts" => provider
                .GetRequiredService<IOptions<UserAccountsPostgreSqlOptions>>().Value.ConnectionString,
            _ => throw new ArgumentOutOfRangeException(nameof(family), family, null)
        };
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;

        public string ApplicationName { get; set; } = "RoyalIdentity.Server.Tests";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } =
            new PhysicalFileProvider(AppContext.BaseDirectory);
    }
}
