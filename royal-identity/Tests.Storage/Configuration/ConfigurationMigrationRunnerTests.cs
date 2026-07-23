using Microsoft.EntityFrameworkCore;
using RoyalIdentity.Data.Configuration.Entities;
using RoyalIdentity.Migrations;
using RoyalIdentity.Security.Keys;
using RoyalIdentity.Storage.EntityFramework.Security.KeyMaterial;
using RoyalIdentity.Storage.EntityFramework.Sqlite;

namespace Tests.Storage.Configuration;

public class ConfigurationMigrationRunnerTests
{
	private static readonly byte[] AesKey = Enumerable.Range(1, 32).Select(value => (byte)value).ToArray();

	[Fact]
	public async Task ProductSeed_Twice_IsIdempotent_AndCreatesUsableProtectedKeys()
	{
		var databasePath = Path.Combine(Path.GetTempPath(), $"royalidentity-config-{Guid.NewGuid():N}.db");
		var keyVariable = $"ROYALIDENTITY_TEST_AES_{Guid.NewGuid():N}";
		Environment.SetEnvironmentVariable(keyVariable, Convert.ToBase64String(AesKey));
		try
		{
			var options = Options(databasePath, ConfigurationSeedMode.Product, keyVariable);
			await ConfigurationMigrationRunner.RunAsync(options);
			await ConfigurationMigrationRunner.RunAsync(options);

			await using (var context = Context(databasePath))
			{
				Assert.Single(context.ServerOptions);
				Assert.Equal(3, await context.Realms.CountAsync());
				Assert.Equal(3, await context.Realms.CountAsync(realm => realm.Internal));
				Assert.Equal(1, await context.Clients.CountAsync(client => client.ClientId == "server_admin"));
				Assert.Equal(3, await context.SigningKeys.CountAsync());
				Assert.False(await context.Realms.AnyAsync(realm => realm.Id == "demo_realm"));
				Assert.Empty(await context.Database.GetPendingMigrationsAsync());

				using var protector = new AesKeyMaterialProtector(
					Microsoft.Extensions.Options.Options.Create(
						new AesKeyMaterialProtectorOptions { Key = AesKey }));
				foreach (var row in await context.SigningKeys.AsNoTracking().ToListAsync())
				{
					Assert.Equal(AesKeyMaterialProtector.Id, row.ProtectorId);
					var envelope = KeyMaterialEnvelope.Parse(row.ProtectorId, row.ProtectedMaterial);
					var material = await protector.UnprotectAsync(envelope);
					var key = ToKey(row, material);
					Assert.NotNull(key.CreateSigningCredentials());
				}
			}
		}
		finally
		{
			Environment.SetEnvironmentVariable(keyVariable, null);
			if (File.Exists(databasePath))
				File.Delete(databasePath);
		}
	}

	[Fact]
	public async Task AllSeed_Twice_KeepsDemoSeparateAndIdempotent()
	{
		var databasePath = Path.Combine(Path.GetTempPath(), $"royalidentity-config-{Guid.NewGuid():N}.db");
		try
		{
			var options = new MigrationRunnerOptions
			{
				ConfigurationProvider = ConfigurationDatabaseProvider.Sqlite,
				ConfigurationConnection = $"Data Source={databasePath};Pooling=False",
				Seed = ConfigurationSeedMode.All,
				KeyProtector = ConfigurationKeyProtector.Plain,
			};
			await ConfigurationMigrationRunner.RunAsync(options);
			await ConfigurationMigrationRunner.RunAsync(options);

			await using (var context = Context(databasePath))
			{
				Assert.Equal(4, await context.Realms.CountAsync());
				Assert.Equal(3, await context.Clients.CountAsync());
				Assert.Equal(4, await context.SigningKeys.CountAsync());
				Assert.Equal(2, await context.Clients.CountAsync(client => client.RealmId == "demo_realm"));
				Assert.All(
					await context.SigningKeys.ToListAsync(),
					key => Assert.Equal(PlainKeyMaterialProtector.Id, key.ProtectorId));
			}
		}
		finally
		{
			if (File.Exists(databasePath))
				File.Delete(databasePath);
		}
	}

	[Fact]
	public void Options_RequireExplicitProviderConnectionAndSeedProtector()
	{
		Assert.Throws<MigrationRunnerUsageException>(() => MigrationRunnerOptions.Parse([]));
		Assert.Throws<MigrationRunnerUsageException>(() => MigrationRunnerOptions.Parse(
			["--configuration-provider", "sqlite", "--configuration-connection", "Data Source=test.db", "--seed", "product"]));
		Assert.Throws<MigrationRunnerUsageException>(() => MigrationRunnerOptions.Parse(
			["--configuration-provider", "unknown", "--configuration-connection", "redacted"]));
	}

	private static MigrationRunnerOptions Options(
		string databasePath,
		ConfigurationSeedMode seed,
		string keyVariable)
		=> new()
		{
			ConfigurationProvider = ConfigurationDatabaseProvider.Sqlite,
			ConfigurationConnection = $"Data Source={databasePath};Pooling=False",
			Seed = seed,
			KeyProtector = ConfigurationKeyProtector.Aes,
			AesKeyEnvironmentVariable = keyVariable,
		};

	private static ConfigurationSqliteDbContext Context(string databasePath)
		=> new(new DbContextOptionsBuilder<ConfigurationSqliteDbContext>()
			.UseSqlite($"Data Source={databasePath};Pooling=False")
			.Options);

	private static KeyParameters ToKey(SigningKeyEntity row, string material)
		=> new(
			row.KeyId,
			row.Name,
			row.SecurityAlgorithm,
			(KeySerializationFormat)row.SerializationFormat,
			(KeyEncoding)row.Encoding,
			material,
			row.CreatedUtc)
		{
			NotBefore = row.NotBeforeUtc,
			Expires = row.ExpiresUtc,
		};
}
