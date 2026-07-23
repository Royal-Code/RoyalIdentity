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
				TimeProvider.System);
			await seed.ApplyAsync(context, options.Seed, ct);
		}
		finally
		{
			if (protector is IDisposable disposable)
				disposable.Dispose();
		}
	}

	private static ConfigurationDbContext CreateContext(MigrationRunnerOptions options)
		=> options.ConfigurationProvider switch
		{
			ConfigurationDatabaseProvider.Sqlite => new ConfigurationSqliteDbContext(
				new DbContextOptionsBuilder<ConfigurationSqliteDbContext>()
					.UseSqlite(options.ConfigurationConnection)
					.Options),
			ConfigurationDatabaseProvider.PostgreSql => new ConfigurationPostgreSqlDbContext(
				new DbContextOptionsBuilder<ConfigurationPostgreSqlDbContext>()
					.UseNpgsql(options.ConfigurationConnection)
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
