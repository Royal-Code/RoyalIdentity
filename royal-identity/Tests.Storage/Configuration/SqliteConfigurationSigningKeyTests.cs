using Microsoft.EntityFrameworkCore;
using RoyalIdentity.Data.Configuration.Entities;
using Tests.Storage.Configuration.Support;

namespace Tests.Storage.Configuration;

/// <summary>
/// Signing-key model checks over the real SQLite schema (plan Fase 2, DF24): the same key id lives
/// independently in two realms (realm-bound composite key), and the create-only primary key rejects a
/// duplicate <c>(realm_id, key_id)</c>. Protector wiring and material handling arrive in Fase 5.
/// </summary>
public class SqliteConfigurationSigningKeyTests
{
	private static readonly DateTime Created = new(2026, 7, 1, 12, 0, 0, DateTimeKind.Utc);

	[Fact]
	public async Task SameKeyId_InTwoRealms_StaysIsolated()
	{
		await using var database = await SqliteConfigurationDatabase.CreateMigratedAsync();

		await using (var context = database.NewContext())
		{
			context.Realms.AddRange(
				ConfigurationTestData.BuildRealmRow("realm-a"),
				ConfigurationTestData.BuildRealmRow("realm-b"));
			context.SigningKeys.Add(BuildKey("realm-a", "key-1", "key in realm A"));
			context.SigningKeys.Add(BuildKey("realm-b", "key-1", "key in realm B"));
			await context.SaveChangesAsync();
		}

		await using (var context = database.NewContext())
		{
			var keyA = await context.SigningKeys.AsNoTracking()
				.SingleAsync(k => k.RealmId == "realm-a" && k.KeyId == "key-1");
			var keyB = await context.SigningKeys.AsNoTracking()
				.SingleAsync(k => k.RealmId == "realm-b" && k.KeyId == "key-1");

			Assert.Equal("key in realm A", keyA.Name);
			Assert.Equal("key in realm B", keyB.Name);
		}
	}

	[Fact]
	public async Task DuplicateKeyId_InSameRealm_IsRejected()
	{
		await using var database = await SqliteConfigurationDatabase.CreateMigratedAsync();

		await using (var context = database.NewContext())
		{
			context.Realms.Add(ConfigurationTestData.BuildRealmRow("realm-a"));
			context.SigningKeys.Add(BuildKey("realm-a", "key-1", "original"));
			await context.SaveChangesAsync();
		}

		await using (var context = database.NewContext())
		{
			context.SigningKeys.Add(BuildKey("realm-a", "key-1", "duplicate"));
			await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
		}
	}

	private static SigningKeyEntity BuildKey(string realmId, string keyId, string name) => new()
	{
		RealmId = realmId,
		KeyId = keyId,
		Name = name,
		SecurityAlgorithm = "ES256",
		SerializationFormat = 0,
		Encoding = 0,
		CreatedUtc = Created,
		ProtectorId = "plain",
		ProtectedMaterial = "opaque-material",
	};
}
