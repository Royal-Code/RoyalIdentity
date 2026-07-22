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

	// RL-03: absent lookup returns null. Load-bearing for realm discovery (404 realm_not_found);
	// final absence semantics per method close in Fase 5 (DF25).
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

	// RL-06: saving an existing realm persists the new configuration and must not destroy the realm's
	// operational data (IRealmManager.UpdateAsync depends on this; the duplicate-write policy per
	// operation is refined in Fase 5 — DF16). A fresh Realm instance with the same id is saved so the
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

	// RL-07: deleting an unknown realm reports false today; final absence semantics close in Fase 5 (DF25).
	[Fact]
	public async Task Delete_UnknownRealm_ReturnsFalse()
	{
		await using var harness = await CreateHarnessAsync();

		var deleted = await harness.Storage.Realms.DeleteAsync("contract-unknown-realm");

		Assert.False(deleted);
	}

	// RL-07 + DF20: observable effects of deleting a common realm, valid for both the fake's hard delete
	// and the future EF tombstone — the realm stops resolving by id/path/domain and its operational data
	// becomes inaccessible. No physical presence (row/tombstone) is inspected.
	[Fact]
	public async Task Delete_CommonRealm_MakesRealmUnresolvable_AndOperationalDataInaccessible()
	{
		await using var harness = await CreateHarnessAsync();
		var realm = await harness.CreateRealmAsync("delete-obs");

		var code = NewAuthorizationCode(realm, "client-del", "subject-del");
		await harness.Storage.GetAuthorizationCodeStore(realm).StoreAuthorizationCodeAsync(code, default);

		var deleted = await harness.Storage.Realms.DeleteAsync(realm.Id);

		Assert.True(deleted);
		Assert.Null(await harness.Storage.Realms.GetByIdAsync(realm.Id, default));
		Assert.Null(await harness.Storage.Realms.GetByPathAsync(realm.Path, default));
		Assert.Null(await harness.Storage.Realms.GetByDomainAsync(realm.Domain, default));

		// Either the realm binding refuses the deleted realm or the lookup finds nothing (EF purge); both
		// satisfy DF20's "active data becomes inaccessible". Only ArgumentException — the binding refusal
		// the contract exhibits today (ST-04..ST-11) — is accepted, so infrastructure failures still fail
		// the test; if Fase 5 (DF25) redefines the refusal signal, this catch is adjusted with it.
		AuthorizationCode? survivor = null;
		try
		{
			survivor = await harness.Storage.GetAuthorizationCodeStore(realm)
				.GetAuthorizationCodeAsync(code.Code, default);
		}
		catch (ArgumentException)
		{
			// binding refusal is an accepted observable effect
		}
		Assert.Null(survivor);
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
