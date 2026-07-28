using RoyalIdentity.Models;
using RoyalIdentity.Models.Tokens;
using Tests.Storage.Operational.Support;
using Tests.Storage.Support;

namespace Tests.Storage.Contracts;

/// <summary>
/// Contract of <c>IRealmStore</c> (matrix RL-01..RL-07): global configuration store for realms.
/// Deletion is asserted at the Configuration boundary: the tombstoned realm stops resolving. Operational purge
/// is a separate maintenance capability because cross-family/admin orchestration remains deliberately deferred.
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

    // RL-07 + DF20: the Configuration owner creates the tombstone and removes the realm from every lookup.
    // Operational purge is covered by OperationalPurgeRealmTests; coordinating it with Configuration and
    // UserAccounts belongs to the deferred admin seam, not IRealmStore.
    [Fact]
    public async Task Delete_CommonRealm_MakesRealmUnresolvable()
    {
        await using var harness = await CreateHarnessAsync();
        var realm = await harness.CreateRealmAsync("delete-obs");

        var deleted = await harness.Storage.Realms.DeleteAsync(realm.Id);

        Assert.True(deleted);
        Assert.Null(await harness.Storage.Realms.GetByIdAsync(realm.Id, default));
        Assert.Null(await harness.Storage.Realms.GetByPathAsync(realm.Path, default));
        Assert.Null(await harness.Storage.Realms.GetByDomainAsync(realm.Domain, default));
    }

    // Provider-specific Configuration tests inspect the permanent tombstone and prove that path/domain remain
    // reserved. This provider-neutral contract observes only the public lookup behavior.

    public sealed class Sqlite : RealmStoreContractTests
    {
        protected override Task<StorageContractHarness> CreateHarnessAsync()
            => SqliteOperationalStorageHarness.CreateAsync();
    }
}
