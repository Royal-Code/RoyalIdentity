using Tests.Storage.Support;

namespace Tests.Storage.Contracts;

/// <summary>
/// Contract of <c>IKeyStore</c> (matrix KY-01..KY-05): key availability windows and historical retention
/// are product rules (`preservar`); listing order by creation is chronological by rule. Fase 5 closed the
/// remaining semantics: absence of <c>GetKeyAsync</c>/<c>GetKeysAsync</c> throws <c>ArgumentException</c>
/// (ids come from the store's own listings — absence signals inconsistency), window boundaries are
/// inclusive (<c>NotBefore &lt;= now &lt;= Expires</c>, <c>null</c> = unbounded), and <c>GetKeysAsync</c>
/// returns keys in the requested order (DF24 rule-backed order).
/// </summary>
public abstract class KeyStoreContractTests : StorageContractTests
{
	// KY-01 + KY-04: an added key is retrievable by id.
	[Fact]
	public async Task AddKey_ThenGetKey_ReturnsTheKey()
	{
		await using var harness = await CreateHarnessAsync();
		var store = harness.Storage.GetKeyStore(harness.RealmA);

		await store.AddKeyAsync(NewKey("contract-key", Start), default);

		var key = await store.GetKeyAsync("contract-key", default);

		Assert.Equal("contract-key", key.KeyId);
	}

	// KY-02 `preservar` (product): only keys currently fit for signing are listed as current.
	[Fact]
	public async Task ListAllCurrentKeysIds_IncludesOnlyKeysValidNow()
	{
		await using var harness = await CreateHarnessAsync();
		var store = harness.Storage.GetKeyStore(harness.RealmA);
		var now = Start;

		await store.AddKeyAsync(NewKey("key-current", now.AddDays(-2),
			notBefore: now.AddDays(-1), expires: now.AddDays(1)), default);
		await store.AddKeyAsync(NewKey("key-unbounded", now.AddDays(-3)), default);
		await store.AddKeyAsync(NewKey("key-expired", now.AddDays(-10),
			notBefore: now.AddDays(-9), expires: now.AddDays(-5)), default);
		await store.AddKeyAsync(NewKey("key-future", now.AddDays(-1),
			notBefore: now.AddDays(5)), default);

		var currentIds = await store.ListAllCurrentKeysIdsAsync(now, default);

		Assert.Contains("key-current", currentIds);
		Assert.Contains("key-unbounded", currentIds);
		Assert.DoesNotContain("key-expired", currentIds);
		Assert.DoesNotContain("key-future", currentIds);
	}

	// KY-02 (Fase 5/DF19 closed): window boundaries are inclusive — a key whose NotBefore equals now and a
	// key whose Expires equals now are both current.
	[Fact]
	public async Task ListAllCurrentKeysIds_WindowBoundaries_AreInclusive()
	{
		await using var harness = await CreateHarnessAsync();
		var store = harness.Storage.GetKeyStore(harness.RealmA);
		var now = Start;

		await store.AddKeyAsync(NewKey("key-nb-boundary", now.AddDays(-1),
			notBefore: now, expires: now.AddDays(1)), default);
		await store.AddKeyAsync(NewKey("key-exp-boundary", now.AddDays(-2),
			notBefore: now.AddDays(-1), expires: now), default);

		var currentIds = await store.ListAllCurrentKeysIdsAsync(now, default);

		Assert.Contains("key-nb-boundary", currentIds);
		Assert.Contains("key-exp-boundary", currentIds);
	}

	// KY-03 `preservar` (product): keys used to sign remain available for validation after expiring;
	// only future keys are excluded.
	[Fact]
	public async Task ListAllKeysIds_IncludesExpiredKeys_AndExcludesFutureKeys()
	{
		await using var harness = await CreateHarnessAsync();
		var store = harness.Storage.GetKeyStore(harness.RealmA);
		var now = Start;

		await store.AddKeyAsync(NewKey("key-current", now.AddDays(-2),
			notBefore: now.AddDays(-1), expires: now.AddDays(1)), default);
		await store.AddKeyAsync(NewKey("key-expired", now.AddDays(-10),
			notBefore: now.AddDays(-9), expires: now.AddDays(-5)), default);
		await store.AddKeyAsync(NewKey("key-future", now.AddDays(-1),
			notBefore: now.AddDays(5)), default);

		var allIds = await store.ListAllKeysIdsAsync(now, default);

		Assert.Contains("key-current", allIds);
		Assert.Contains("key-expired", allIds);
		Assert.DoesNotContain("key-future", allIds);
	}

	// KY-03 (Fase 5/DF19): the historical-key lower boundary is inclusive — a key becomes available for
	// validation exactly at NotBefore.
	[Fact]
	public async Task ListAllKeysIds_NotBeforeBoundary_IsInclusive()
	{
		await using var harness = await CreateHarnessAsync();
		var store = harness.Storage.GetKeyStore(harness.RealmA);
		var now = Start;

		await store.AddKeyAsync(NewKey("key-history-nb-boundary", now.AddDays(-1), notBefore: now), default);

		var allIds = await store.ListAllKeysIdsAsync(now, default);

		Assert.Contains("key-history-nb-boundary", allIds);
	}

	// KY-02/KY-03 `preservar`: listings are ordered chronologically by creation (rule-backed order — DF24).
	[Fact]
	public async Task KeyListings_AreOrderedByCreation()
	{
		await using var harness = await CreateHarnessAsync();
		var store = harness.Storage.GetKeyStore(harness.RealmA);

		await store.AddKeyAsync(NewKey("key-third", Start.AddHours(-1)), default);
		await store.AddKeyAsync(NewKey("key-first", Start.AddHours(-3)), default);
		await store.AddKeyAsync(NewKey("key-second", Start.AddHours(-2)), default);

		var currentIds = await store.ListAllCurrentKeysIdsAsync(Start, default);

		Assert.Equal(["key-first", "key-second", "key-third"], currentIds);
	}

	// KY-05 (Fase 5/DF24): the result comes in the order of the requested ids — a rule-backed order
	// (correspondence to the request), not creation or backing order.
	[Fact]
	public async Task GetKeys_ReturnsKeysInRequestedOrder()
	{
		await using var harness = await CreateHarnessAsync();
		var store = harness.Storage.GetKeyStore(harness.RealmA);

		await store.AddKeyAsync(NewKey("key-one", Start.AddHours(-2)), default);
		await store.AddKeyAsync(NewKey("key-two", Start.AddHours(-1)), default);

		var keys = await store.GetKeysAsync(["key-two", "key-one"], default);

		Assert.Equal(["key-two", "key-one"], keys.Select(k => k.KeyId));
	}

	// KY-04 (Fase 5/DF25): absence throws — key ids come from the store's own listings, so a missing id
	// signals inconsistency, not normal flow. ArgumentException is the parity signal (current contract).
	[Fact]
	public async Task GetKey_UnknownKeyId_ThrowsArgumentException()
	{
		await using var harness = await CreateHarnessAsync();

		await Assert.ThrowsAsync<ArgumentException>(() =>
			harness.Storage.GetKeyStore(harness.RealmA).GetKeyAsync("contract-unknown-key", default));
	}

	// DF18: key ids are opaque and compare Ordinal. The key-lookup absence contract remains the KY-04
	// ArgumentException outlier.
	[Fact]
	public async Task GetKey_KeyIdDifferingOnlyByCase_ThrowsArgumentException()
	{
		await using var harness = await CreateHarnessAsync();
		var store = harness.Storage.GetKeyStore(harness.RealmA);
		await store.AddKeyAsync(NewKey("contract-case-key", Start), default);

		await Assert.ThrowsAsync<ArgumentException>(() =>
			store.GetKeyAsync("CONTRACT-CASE-KEY", default));
	}

	// DF6: the same key id in two realms resolves to each realm's own key material.
	[Fact]
	public async Task SameKeyId_InTwoRealms_ReturnsEachRealmsOwnKey()
	{
		await using var harness = await CreateHarnessAsync();

		await harness.Storage.GetKeyStore(harness.RealmA)
			.AddKeyAsync(NewKey("contract-shared-key", Start, name: "Key of realm A"), default);
		await harness.Storage.GetKeyStore(harness.RealmB)
			.AddKeyAsync(NewKey("contract-shared-key", Start, name: "Key of realm B"), default);

		var inA = await harness.Storage.GetKeyStore(harness.RealmA).GetKeyAsync("contract-shared-key", default);
		var inB = await harness.Storage.GetKeyStore(harness.RealmB).GetKeyAsync("contract-shared-key", default);

		Assert.Equal("Key of realm A", inA.Name);
		Assert.Equal("Key of realm B", inB.Name);
	}

	// DF6: keys added in one realm are not listed in another.
	[Fact]
	public async Task KeysAddedInOneRealm_AreNotListedInAnotherRealm()
	{
		await using var harness = await CreateHarnessAsync();

		await harness.Storage.GetKeyStore(harness.RealmA)
			.AddKeyAsync(NewKey("contract-only-a-key", Start), default);

		var idsInB = await harness.Storage.GetKeyStore(harness.RealmB).ListAllKeysIdsAsync(Start, default);

		Assert.DoesNotContain("contract-only-a-key", idsInB);
	}

	public sealed class InMemory : KeyStoreContractTests
	{
		protected override Task<StorageContractHarness> CreateHarnessAsync() => InMemoryStorageHarness.CreateAsync();
	}

	public sealed class Sqlite : KeyStoreContractTests
	{
		protected override Task<StorageContractHarness> CreateHarnessAsync()
			=> Configuration.Support.SqliteConfigurationStorageHarness.CreateAsync();
	}
}
