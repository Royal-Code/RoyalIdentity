using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using RoyalIdentity.Data.Configuration;
using RoyalIdentity.Storage.EntityFramework.Configuration.Materialization;
using RoyalIdentity.Storage.EntityFramework.PostgreSql;
using RoyalIdentity.Storage.EntityFramework.Security.KeyMaterial;
using RoyalIdentity.Storage.EntityFramework.Sqlite;

namespace RoyalIdentity.Migrations;

public static class ConfigurationMigrationRunner
{
    public static async Task RunAsync(MigrationRunnerOptions options, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (options.Seed.HasFlag(ConfigurationSeedMode.Product))
            options.ProductSeed.Validate();

        // Operational plan DF23: a database migrated by Plano 2 still carries EF's default history. Relocate it
        // BEFORE EF consults the newly configured one, or the history looks empty and every table is recreated.
        await BootstrapMigrationsHistoryAsync(options, ct);

        await using var context = CreateContext(options);
        await context.Database.MigrateAsync(ct);

        if (options.Seed is ConfigurationSeedMode.None)
            return;

        var protector = CreateProtector(options);
        try
        {
            var seed = new ConfigurationSeed(
                new ServerOptionsPayloadSerializer(),
                new RealmOptionsPayloadSerializer(),
                new ClientMaterializer(),
                protector,
                TimeProvider.System,
                options.ProductSeed);
            await seed.ApplyAsync(context, options.Seed, ct);
        }
        finally
        {
            if (protector is IDisposable disposable)
                disposable.Dispose();
        }
    }

    /// <summary>
    /// Moves a legacy <c>__EFMigrationsHistory</c> onto the Configuration history of DF23 — by name on SQLite,
    /// by schema on PostgreSQL. Idempotent on both, and both fail closed when the two histories coexist.
    /// </summary>
    private static async Task BootstrapMigrationsHistoryAsync(MigrationRunnerOptions options, CancellationToken ct)
    {
        switch (options.ConfigurationProvider)
        {
            case ConfigurationDatabaseProvider.Sqlite:
                await new SqliteMigrationsHistoryBootstrap().RunAsync(
                    options.ResolvedConfigurationConnection, ct);
                break;

            case ConfigurationDatabaseProvider.PostgreSql:
                await new PostgreSqlMigrationsHistoryBootstrap().RunAsync(
                    options.ResolvedConfigurationConnection, ct);
                break;

            default:
                throw new InvalidOperationException("Unsupported Configuration provider.");
        }
    }

    private static ConfigurationDbContext CreateContext(MigrationRunnerOptions options)
        => options.ConfigurationProvider switch
        {
            ConfigurationDatabaseProvider.Sqlite => new ConfigurationSqliteDbContext(
                new DbContextOptionsBuilder<ConfigurationSqliteDbContext>()
                    .UseSqlite(
                        options.ResolvedConfigurationConnection,
                        sqlite => sqlite.UseConfigurationMigrationsHistory())
                    .Options),
            ConfigurationDatabaseProvider.PostgreSql => new ConfigurationPostgreSqlDbContext(
                new DbContextOptionsBuilder<ConfigurationPostgreSqlDbContext>()
                    .UseNpgsql(
                        options.ResolvedConfigurationConnection,
                        npgsql => npgsql.UseConfigurationMigrationsHistory())
                    .Options),
            _ => throw new InvalidOperationException("Unsupported Configuration provider."),
        };

    private static IKeyMaterialProtector CreateProtector(MigrationRunnerOptions options)
        => options.KeyProtector switch
        {
            ConfigurationKeyProtector.Plain => new PlainKeyMaterialProtector(
                new MigrationRunnerConsoleLogger<PlainKeyMaterialProtector>()),
            ConfigurationKeyProtector.Aes => CreateAesProtector(options),
            ConfigurationKeyProtector.DataProtection => new AspNetDataProtectionKeyMaterialProtector(
                DataProtectionProvider.Create(
                    new DirectoryInfo(options.DataProtectionKeyRing!),
                    builder => builder.SetApplicationName(options.DataProtectionApplicationName))),
            _ => throw new InvalidOperationException("A signing-key protector is required for seed execution."),
        };

    private static AesKeyMaterialProtector CreateAesProtector(MigrationRunnerOptions options)
    {
        var key = ReadAesKey(options);
        try
        {
            return new AesKeyMaterialProtector(
                Microsoft.Extensions.Options.Options.Create(
                    new AesKeyMaterialProtectorOptions { Key = key }));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
        }
    }

    private static byte[] ReadAesKey(MigrationRunnerOptions options)
    {
        var encoded = Environment.GetEnvironmentVariable(options.AesKeyEnvironmentVariable!);
        if (string.IsNullOrWhiteSpace(encoded))
        {
            throw new InvalidOperationException(
                $"AES key environment variable '{options.AesKeyEnvironmentVariable}' is missing or empty.");
        }

        try
        {
            return Convert.FromBase64String(encoded);
        }
        catch (FormatException exception)
        {
            throw new InvalidOperationException("The configured AES key is not valid Base64.", exception);
        }
    }
}
