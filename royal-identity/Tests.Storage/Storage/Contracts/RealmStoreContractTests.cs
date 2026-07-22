using RoyalIdentity.Models;
using RoyalIdentity.Models.Tokens;
using Tests.Storage.Support;

namespace Tests.Storage.Contracts;

/// <summary>
/// Contract of <c>IRealmStore</c> (matrix RL-01..RL-07): global configuration store for realms.
/// Deletion is asserted only by its observable effects, common to the fake's hard delete and the EF
/// tombstone target (DF20): the realm stops resolving and its operational data becomes inaccessible.
/// </summary>
public abstract class RealmStoreContractTests : StorageContractTests
{
	// RL-06 + RL-01/RL-03/RL-04: a saved realm is resolvable by every lookup key.
	[Fact]
	public async Task Save_NewRealm_IsFoundByIdPathAndDomain()
	{
		await using var harness = await CreateHarnessAsync();
		var realm = await harness.CreateRealmAsync("save-lookup");

		var byId = await harness.Storage.Realms.GetByIdAsync(realm.Id, default);
		var byPath = await harness.Storage.Realms.GetByPathAsync(realm.Path, default);
		var byDomain = await harness.Storage.Realms.GetByDomainAsync(realm.Domain, default);

		Assert.NotNull(byId);
		Assert.Equal(realm.Id, byId.Id);
		Assert.NotNull(byPath);
		Assert.Equal(realm.Id, byPath.Id);
		Assert.NotNull(byDomain);
		Assert.Equal(realm.Id, byDomain.Id);
	}

	// RL-03 (Fase 5/DF25 closed): absent lookup returns null. Load-bearing for realm discovery
	// (404 realm_not_found).
	[Fact]
	public async Task GetById_UnknownRealm_ReturnsNull()
	{
		await using var harness = await CreateHarnessAsync();

		var realm = await harness.Storage.Realms.GetByIdAsync("contract-unknown-realm", default);

		Assert.Null(realm);
	}

	// RL-01: absent path lookup returns null (same DF25 note as above).
	[Fact]
	public async Task GetByPath_UnknownPath_ReturnsNull()
	{
		await using var harness = await CreateHarnessAsync();

		var realm = await harness.Storage.Realms.GetByPathAsync("contract-unknown-path", default);

		Assert.Null(realm);
	}

	// RL-04 (Fase 5/DF25): absent domain lookup returns null. Domain comparison is Ordinal over the
	// value normalized (lowercase) at the write edge — never provider collation (DF18).
	[Fact]
	public async Task GetByDomain_UnknownDomain_ReturnsNull()
	{
		await using var harness = await CreateHarnessAsync();

		var realm = await harness.Storage.Realms.GetByDomainAsync("unknown.contract.test", default);

		Assert.Null(realm);
	}

	// DF18 (Fase 5): path and domain lookups are Ordinal at the store — values differing only by casing do
	// not match. Domain normalization (lowercase) belongs to the edges (MP-10, new behavior for the EF
	// provider/manager), never to the store or provider collation.
	[Fact]
	public async Task GetByIdPathOrDomain_DifferingOnlyByCase_ReturnsNull()
	{
		await using var harness = await CreateHarnessAsync();
		var realm = await harness.CreateRealmAsync("case");

		Assert.Null(await harness.Storage.Realms.GetByIdAsync(realm.Id.ToUpperInvariant(), default));
		Assert.NotNull(await harness.Storage.Realms.GetByPathAsync(realm.Path, default));
		Assert.Null(await harness.Storage.Realms.GetByPathAsync(realm.Path.ToUpperInvariant(), default));
		Assert.Null(await harness.Storage.Realms.GetByDomainAsync(realm.Domain.ToUpperInvariant(), default));
	}

	// RL-06 (Fase 5/DF16 closed: SaveAsync is upsert by method semantics): saving an existing realm
	// persists the new configuration and must not destroy the realm's operational data
	// (IRealmManager.UpdateAsync depends on this). A fresh Realm instance with the same id is saved so the
	// assertion cannot be satisfied by mutating a live reference already held by the backing (DF17).
	[Fact]
	public async Task Save_ExistingRealm_UpdatesConfiguration_AndKeepsOperationalData()
	{
		await using var harness = await CreateHarnessAsync();
		var realm = await harness.CreateRealmAsync("save-update");

		var code = NewAuthorizationCode(realm, "client-x", "subject-x");
		await harness.Storage.GetAuthorizationCodeStore(realm).StoreAuthorizationCodeAsync(code, default);

		var updatedRealm = new Realm(realm.Id, realm.Domain, realm.Path, "Contract Realm updated", false,
			realm.Options);
		await harness.Storage.Realms.SaveAsync(updatedRealm);

		var updated = await harness.Storage.Realms.GetByIdAsync(realm.Id, default);
		var survivingCode = await harness.Storage.GetAuthorizationCodeStore(realm)
			.GetAuthorizationCodeAsync(code.Code, default);

		Assert.NotNull(updated);
		Assert.Equal("Contract Realm updated", updated.DisplayName);
		Assert.NotNull(survivingCode);
	}

	// RL-05 + DF24: enumeration is a set — saved realms must appear; no order is contractual.
	[Fact]
	public async Task GetAll_ContainsSavedRealms()
	{
		await using var harness = await CreateHarnessAsync();
		var realmC = await harness.CreateRealmAsync("all-c");
		var realmD = await harness.CreateRealmAsync("all-d");

		var ids = new HashSet<string>();
		await foreach (var realm in harness.Storage.Realms.GetAllAsync(default))
			ids.Add(realm.Id);

		Assert.Contains(realmC.Id, ids);
		Assert.Contains(realmD.Id, ids);
		Assert.Contains(harness.RealmA.Id, ids);
		Assert.Contains(harness.RealmB.Id, ids);
	}

	// RL-07 `preservar` + invariante 8: internal realms are never removable.
	[Fact]
	public async Task Delete_InternalRealm_IsRefused_AndRealmRemainsResolvable()
	{
		await using var harness = await CreateHarnessAsync();

		var deleted = await harness.Storage.Realms.DeleteAsync(harness.InternalRealm.Id);

		Assert.False(deleted);
		var stillThere = await harness.Storage.Realms.GetByIdAsync(harness.InternalRealm.Id, default);
		Assert.NotNull(stillThere);
	}

	// RL-07 (Fase 5/DF25 closed): deleting an unknown realm reports false, idempotently.
	[Fact]
	public async Task Delete_UnknownRealm_ReturnsFalse()
	{
		await using var harness = await CreateHarnessAsync();

		var deleted = await harness.Storage.Realms.DeleteAsync("contract-unknown-realm");

		Assert.False(deleted);
	}

	// RL-07 + DF20: observable effects of deleting a common realm, valid for both the fake's hard delete
	// and the future EF tombstone — the realm stops resolving by id/path/domain and data previously stored
	// in EVERY realm-bound store (ST-04..ST-11) becomes inaccessible. No physical presence (row/tombstone)
	// is inspected.
	[Fact]
	public async Task Delete_CommonRealm_MakesRealmUnresolvable_AndAllRealmBoundDataInaccessible()
	{
		await using var harness = await CreateHarnessAsync();
		var realm = await harness.CreateRealmAsync("delete-obs");

		await harness.SeedClientAsync(NewClient(realm, "del-client"));
		await harness.SeedIdentityScopeAsync(realm, NewIdentityScope("contract:del-scope"));
		await harness.Storage.GetKeyStore(realm).AddKeyAsync(NewKey("del-key", Start), default);
		await harness.Storage.GetAccessTokenStore(realm)
			.StoreAsync(NewAccessToken(realm, "del-jti", "del-client"), default);
		await harness.Storage.GetRefreshTokenStore(realm)
			.StoreAsync(NewRefreshToken(realm, "del-handle", "del-subject", "del-client"), default);
		var code = NewAuthorizationCode(realm, "del-client", "del-subject");
		await harness.Storage.GetAuthorizationCodeStore(realm).StoreAuthorizationCodeAsync(code, default);
		await harness.Storage.GetUserConsentStore(realm)
			.StoreUserConsentAsync(NewConsent(realm, "del-subject", "del-client", "openid"), default);
		await harness.Storage.GetUserSessionStore(realm).CreateAsync(NewSession("del-sid", "del-subject"));

		var deleted = await harness.Storage.Realms.DeleteAsync(realm.Id);

		Assert.True(deleted);
		Assert.Null(await harness.Storage.Realms.GetByIdAsync(realm.Id, default));
		Assert.Null(await harness.Storage.Realms.GetByPathAsync(realm.Path, default));
		Assert.Null(await harness.Storage.Realms.GetByDomainAsync(realm.Domain, default));

		// Every realm-bound accessor (ST-04..ST-11) must refuse the binding or find nothing (EF purge).
		Assert.Null(await ProbeAsync(async () =>
			await harness.Storage.GetClientStore(realm).FindClientByIdAsync("del-client", default)));
		Assert.Null(await ProbeAsync(async () =>
		{
			var resources = await harness.Storage.GetResourceStore(realm)
				.FindResourcesByScopeAsync(["contract:del-scope"], onlyEnabled: false, default);
			return resources.IdentityScopes.FirstOrDefault(s => s.Name == "contract:del-scope");
		}));
		Assert.Null(await ProbeAsync(async () =>
			await harness.Storage.GetKeyStore(realm).GetKeyAsync("del-key", default)));
		Assert.Null(await ProbeAsync(async () =>
			await harness.Storage.GetAccessTokenStore(realm).GetAsync("del-jti", default)));
		Assert.Null(await ProbeAsync(async () =>
			await harness.Storage.GetRefreshTokenStore(realm).GetAsync("del-handle", default)));
		Assert.Null(await ProbeAsync(async () =>
			await harness.Storage.GetAuthorizationCodeStore(realm).GetAuthorizationCodeAsync(code.Code, default)));
		Assert.Null(await ProbeAsync(async () =>
			await harness.Storage.GetUserConsentStore(realm).GetUserConsentAsync("del-subject", "del-client", default)));
		Assert.Null(await ProbeAsync(async () =>
			await harness.Storage.GetUserSessionStore(realm).FindByIdAsync("del-sid", default)));
	}

	// ArgumentException is the fake's current binding-refusal signal. It is an accepted observable effect of
	// DF20, but not required from a synchronous EF accessor; returning no data after purge is equally valid.
	// Any other exception is an infrastructure failure and fails the test.
	private static async Task<T?> ProbeAsync<T>(Func<Task<T?>> read) where T : class
	{
		try
		{
			return await read();
		}
		catch (ArgumentException)
		{
			return null;
		}
	}

	// ── Plano 2 acceptance (registered, NOT tested against the transitional fake — ADR-018/DF20): ──
	// after deletion the EF provider keeps a permanent Configuration tombstone, invisible to normal lookups,
	// and the deleted realm's path and domain remain reserved (SaveAsync/creation of a new realm reusing them
	// must be refused). The fake removes physically and allows reuse; forcing parity here is forbidden.
	// The EF configuration provider must add a scenario covering path/domain reservation in Plano 2.

	public sealed class InMemory : RealmStoreContractTests
	{
		protected override Task<StorageContractHarness> CreateHarnessAsync() => InMemoryStorageHarness.CreateAsync();
	}
}
