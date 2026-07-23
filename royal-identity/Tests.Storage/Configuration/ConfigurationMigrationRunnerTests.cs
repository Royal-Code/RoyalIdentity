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
	private static readonly string[] ProductRedirectUris =
	[
		"https://admin.example.test/signin-oidc",
		"https://admin.example.test/callback",
	];

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
				var serverAdmin = await context.Clients.SingleAsync(client => client.ClientId == "server_admin");
				Assert.Equal("server", serverAdmin.RealmId);
				Assert.True(serverAdmin.Enabled);
				Assert.True(serverAdmin.RequirePkce);
				Assert.False(serverAdmin.RequireClientSecret);
				Assert.True(serverAdmin.AllowOfflineAccess);
				Assert.Empty(await context.ClientSecrets.Where(secret => secret.ClientId == "server_admin").ToListAsync());

				var stringValues = await context.ClientStringValues
					.Where(value => value.ClientId == "server_admin")
					.ToListAsync();
				Assert.Equal(
					ProductRedirectUris.Order(StringComparer.Ordinal),
					stringValues
						.Where(value => value.Kind == ClientStringValueKinds.RedirectUri)
						.Select(value => value.Value)
						.Order(StringComparer.Ordinal));
				Assert.Equal(
					["openid", "profile"],
					stringValues
						.Where(value => value.Kind == ClientStringValueKinds.AllowedIdentityScope)
						.Select(value => value.Value)
						.Order(StringComparer.Ordinal));
				Assert.Contains(
					stringValues,
					value => value.Kind == ClientStringValueKinds.AllowedGrantType
						&& value.Value == "authorization_code");
				Assert.Contains(
					stringValues,
					value => value.Kind == ClientStringValueKinds.AllowedResponseType
						&& value.Value == "code");
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
				ProductSeed = ProductSeedOptions(),
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

	[Fact]
	public void Options_RequireExplicitProductRedirectUris_AndAcceptRepeatedValues()
	{
		var baseArguments = new[]
		{
			"--configuration-provider", "sqlite",
			"--configuration-connection", "Data Source=test.db",
			"--seed", "product",
			"--key-protector", "plain",
		};

		Assert.Throws<MigrationRunnerUsageException>(() => MigrationRunnerOptions.Parse(baseArguments));
		Assert.Throws<MigrationRunnerUsageException>(() => MigrationRunnerOptions.Parse(
			[.. baseArguments, "--server-admin-redirect-uri", "relative/callback"]));
		Assert.Throws<MigrationRunnerUsageException>(() => MigrationRunnerOptions.Parse(
			[
				.. baseArguments,
				"--server-admin-redirect-uri", ProductRedirectUris[0],
				"--server-admin-redirect-uri", ProductRedirectUris[0],
			]));
		Assert.Throws<MigrationRunnerUsageException>(() => MigrationRunnerOptions.Parse(
			[
				"--configuration-provider", "sqlite",
				"--configuration-connection", "Data Source=test.db",
				"--seed", "demo",
				"--key-protector", "plain",
				"--server-admin-redirect-uri", ProductRedirectUris[0],
			]));

		var options = MigrationRunnerOptions.Parse(
			[
				.. baseArguments,
				"--server-admin-redirect-uri", ProductRedirectUris[0],
				"--server-admin-redirect-uri", ProductRedirectUris[1],
			]);

		Assert.Equal(ProductRedirectUris, options.ProductSeed.ServerAdminRedirectUris);
	}

	[Fact]
	public async Task ProductSeed_WithoutRedirectUris_FailsBeforeOpeningDatabase()
	{
		var databasePath = Path.Combine(Path.GetTempPath(), $"royalidentity-config-{Guid.NewGuid():N}.db");
		try
		{
			var options = new MigrationRunnerOptions
			{
				ConfigurationProvider = ConfigurationDatabaseProvider.Sqlite,
				ConfigurationConnection = $"Data Source={databasePath};Pooling=False",
				Seed = ConfigurationSeedMode.Product,
				KeyProtector = ConfigurationKeyProtector.Plain,
			};

			await Assert.ThrowsAsync<InvalidOperationException>(() => ConfigurationMigrationRunner.RunAsync(options));
			Assert.False(File.Exists(databasePath));
		}
		finally
		{
			if (File.Exists(databasePath))
				File.Delete(databasePath);
		}
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
			ProductSeed = ProductSeedOptions(),
			AesKeyEnvironmentVariable = keyVariable,
		};

	private static ConfigurationProductSeedOptions ProductSeedOptions()
		=> new() { ServerAdminRedirectUris = ProductRedirectUris };

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
