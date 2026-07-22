using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.IdentityModel.Tokens;
using RoyalIdentity.Contracts;
using RoyalIdentity.Contracts.Defaults;
using RoyalIdentity.Contracts.Storage;
using RoyalIdentity.Models;
using RoyalIdentity.Security.Keys;
using RoyalIdentity.Utils.Caching;
using Tests.Storage.Configuration.Support;
using Tests.Storage.Support;

namespace Tests.Storage.Configuration;

public class SigningKeyStartupValidatorTests
{
	[Fact]
	public async Task StartAsync_WithUsableCurrentMainAlgorithmKey_Succeeds()
	{
		await using var harness = await PrepareSingleEnabledRealmAsync();
		await harness.Storage.GetKeyStore(harness.RealmA)
			.AddKeyAsync(CreateUsableKey(harness.RealmA), default);
		await using var services = CreateValidatorServices(harness);

		await services.GetRequiredService<SigningKeyStartupValidator>().StartAsync(default);
	}

	[Fact]
	public async Task StartAsync_WithMissingKey_FailsBeforeServingRequests()
	{
		await using var harness = await PrepareSingleEnabledRealmAsync();
		await using var services = CreateValidatorServices(harness);

		var exception = await Assert.ThrowsAsync<InvalidOperationException>(
			() => services.GetRequiredService<SigningKeyStartupValidator>().StartAsync(default));

		Assert.Contains(harness.RealmA.Id, exception.Message, StringComparison.Ordinal);
		Assert.Null(exception.InnerException);
	}

	[Fact]
	public async Task StartAsync_WithCorruptedCiphertext_FailsBeforeServingRequests()
	{
		await using var harness = await PrepareSingleEnabledRealmAsync();
		var key = CreateUsableKey(harness.RealmA);
		await harness.Storage.GetKeyStore(harness.RealmA).AddKeyAsync(key, default);
		var row = await harness.DbContext.SigningKeys.SingleAsync(entity => entity.KeyId == key.KeyId);
		row.ProtectedMaterial = row.ProtectedMaterial[..^1] + (row.ProtectedMaterial[^1] == 'A' ? "B" : "A");
		await harness.DbContext.SaveChangesAsync();
		harness.DbContext.ChangeTracker.Clear();
		await using var services = CreateValidatorServices(harness);

		var exception = await Assert.ThrowsAsync<InvalidOperationException>(
			() => services.GetRequiredService<SigningKeyStartupValidator>().StartAsync(default));

		Assert.NotNull(exception.InnerException);
		Assert.DoesNotContain(key.Key, exception.ToString(), StringComparison.Ordinal);
	}

	[Fact]
	public async Task StartAsync_WithIncompatibleProtector_FailsBeforeServingRequests()
	{
		await using var harness = await PrepareSingleEnabledRealmAsync();
		var key = CreateUsableKey(harness.RealmA);
		await harness.Storage.GetKeyStore(harness.RealmA).AddKeyAsync(key, default);
		var row = await harness.DbContext.SigningKeys.SingleAsync(entity => entity.KeyId == key.KeyId);
		row.ProtectorId = "unavailable-protector";
		await harness.DbContext.SaveChangesAsync();
		harness.DbContext.ChangeTracker.Clear();
		await using var services = CreateValidatorServices(harness);

		var exception = await Assert.ThrowsAsync<InvalidOperationException>(
			() => services.GetRequiredService<SigningKeyStartupValidator>().StartAsync(default));

		Assert.NotNull(exception.InnerException);
		Assert.DoesNotContain(key.Key, exception.ToString(), StringComparison.Ordinal);
	}

	[Fact]
	public async Task StartAsync_WithOnlyDifferentAlgorithm_FailsBeforeServingRequests()
	{
		await using var harness = await PrepareSingleEnabledRealmAsync();
		var key = CreateUsableKey(harness.RealmA, SecurityAlgorithms.RsaSha256);
		await harness.Storage.GetKeyStore(harness.RealmA).AddKeyAsync(key, default);
		await using var services = CreateValidatorServices(harness);

		var exception = await Assert.ThrowsAsync<InvalidOperationException>(
			() => services.GetRequiredService<SigningKeyStartupValidator>().StartAsync(default));

		Assert.Contains("main algorithm", exception.Message, StringComparison.Ordinal);
		Assert.DoesNotContain(key.Key, exception.ToString(), StringComparison.Ordinal);
	}

	private static async Task<SqliteConfigurationStorageHarness> PrepareSingleEnabledRealmAsync()
	{
		var harness = await SqliteConfigurationStorageHarness.CreateConcreteAsync();
		var realms = new List<Realm>();
		await foreach (var realm in harness.Storage.Realms.GetAllAsync(default))
			realms.Add(realm);

		foreach (var realm in realms.Where(realm => realm.Id != harness.RealmA.Id))
		{
			realm.Enabled = false;
			await harness.Storage.Realms.SaveAsync(realm);
		}

		return harness;
	}

	private static KeyParameters CreateUsableKey(Realm realm, string? algorithm = null)
	{
		var key = KeyMaterialFactory.Create(algorithm ?? realm.Options.Keys.MainSigningCredentialsAlgorithm);
		key.Created = StorageContractHarness.Start.AddHours(-2);
		key.NotBefore = StorageContractHarness.Start.AddHours(-1);
		key.Expires = StorageContractHarness.Start.AddHours(1);
		return key;
	}

	private static ServiceProvider CreateValidatorServices(SqliteConfigurationStorageHarness harness)
	{
		var keyManager = new DefaultKeyManager(
			harness.Storage,
			new RealmCaching(harness.Provider),
			NullLogger<DefaultKeyManager>.Instance);
		var services = new ServiceCollection();
		services.AddSingleton<IStorage>(harness.Storage);
		services.AddSingleton<IKeyManager>(keyManager);
		services.AddTransient<SigningKeyStartupValidator>();
		return services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true });
	}
}
