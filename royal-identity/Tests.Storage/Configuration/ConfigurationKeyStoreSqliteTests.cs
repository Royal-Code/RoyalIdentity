using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using RoyalIdentity.Data.Configuration;
using RoyalIdentity.Security.Keys;
using RoyalIdentity.Storage.EntityFramework.Security.KeyMaterial;
using RoyalIdentity.Storage.EntityFramework.Sqlite;
using Tests.Storage.Configuration.Support;
using Tests.Storage.Support;

namespace Tests.Storage.Configuration;

public abstract class ConfigurationKeyStoreProviderTests<TContext>
	where TContext : ConfigurationDbContext
{
	private protected abstract Task<ConfigurationStorageHarness<TContext>> CreateHarnessAsync();

	[Fact]
	public async Task AddKey_ProtectsMaterial_AndReturnsIndependentGraphs()
	{
		await using var harness = await CreateHarnessAsync();
		var key = NewKey("protected-key", "highly-sensitive-key-material");
		var store = harness.Storage.GetKeyStore(harness.RealmA);

		await store.AddKeyAsync(key, default);
		harness.DbContext.ChangeTracker.Clear();
		var row = await harness.DbContext.SigningKeys.AsNoTracking().SingleAsync(entity => entity.KeyId == key.KeyId);

		Assert.Equal(AesKeyMaterialProtector.Id, row.ProtectorId);
		Assert.DoesNotContain(key.Key, row.ProtectedMaterial, StringComparison.Ordinal);
		var first = await store.GetKeyAsync(key.KeyId, default);
		first.Created = StorageContractHarness.Start.AddYears(1);
		var second = await store.GetKeyAsync(key.KeyId, default);
		Assert.Equal(key.Key, second.Key);
		Assert.Equal(StorageContractHarness.Start, second.Created);
	}

	[Fact]
	public async Task AddKey_DuplicateIdFails_AndDoesNotOverwriteOriginal()
	{
		await using var harness = await CreateHarnessAsync();
		var store = harness.Storage.GetKeyStore(harness.RealmA);
		await store.AddKeyAsync(NewKey("duplicate", "original-material"), default);

		await Assert.ThrowsAsync<DbUpdateException>(
			() => store.AddKeyAsync(NewKey("duplicate", "replacement-material"), default));

		var stored = await store.GetKeyAsync("duplicate", default);
		Assert.Equal("original-material", stored.Key);
	}

	[Fact]
	public async Task GetKey_CorruptedCiphertextFailsClosed_WithoutMaterialInException()
	{
		await using var harness = await CreateHarnessAsync();
		var key = NewKey("corrupted", "material-not-for-error-message");
		var store = harness.Storage.GetKeyStore(harness.RealmA);
		await store.AddKeyAsync(key, default);

		var row = await harness.DbContext.SigningKeys.SingleAsync(entity => entity.KeyId == key.KeyId);
		var envelope = KeyMaterialEnvelope.Parse(row.ProtectorId, row.ProtectedMaterial);
		var bytes = Convert.FromBase64String(envelope.Payload);
		bytes[^1] ^= 0x01;
		row.ProtectedMaterial = new KeyMaterialEnvelope(row.ProtectorId, Convert.ToBase64String(bytes))
			.ToPersistedPayload();
		await harness.DbContext.SaveChangesAsync();
		harness.DbContext.ChangeTracker.Clear();

		var exception = await Assert.ThrowsAsync<AuthenticationTagMismatchException>(
			() => store.GetKeyAsync(key.KeyId, default));
		Assert.DoesNotContain(key.Key, exception.ToString(), StringComparison.Ordinal);
	}

	[Fact]
	public async Task KeyOperations_PropagateCancellation()
	{
		await using var harness = await CreateHarnessAsync();
		var store = harness.Storage.GetKeyStore(harness.RealmA);
		using var cancellation = new CancellationTokenSource();
		await cancellation.CancelAsync();

		await Assert.ThrowsAnyAsync<OperationCanceledException>(
			() => store.ListAllCurrentKeysIdsAsync(ct: cancellation.Token));
		await Assert.ThrowsAnyAsync<OperationCanceledException>(
			() => store.GetKeyAsync("missing", cancellation.Token));
		await Assert.ThrowsAnyAsync<OperationCanceledException>(
			() => store.AddKeyAsync(NewKey("cancelled", "cancelled-material"), cancellation.Token));
	}

	private static KeyParameters NewKey(string keyId, string material)
		=> new(
			keyId,
			$"Key {keyId}",
			"RS256",
			KeySerializationFormat.Json,
			KeyEncoding.Plain,
			material,
			StorageContractHarness.Start);
}

public sealed class ConfigurationKeyStoreSqliteTests
	: ConfigurationKeyStoreProviderTests<ConfigurationSqliteDbContext>
{
	private protected override async Task<ConfigurationStorageHarness<ConfigurationSqliteDbContext>>
		CreateHarnessAsync()
		=> await SqliteConfigurationStorageHarness.CreateConcreteAsync();
}
