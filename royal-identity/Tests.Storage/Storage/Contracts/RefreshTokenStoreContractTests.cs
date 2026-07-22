using Tests.Storage.Support;

namespace Tests.Storage.Contracts;

/// <summary>
/// Contract of <c>IRefreshTokenStore</c> (matrix RT-01..RT-05). RT-03 (<c>UpdateAsync</c>) has no scenario
/// here on purpose: the fake's live-reference backing makes any persistence assertion pass trivially (a
/// mutation is visible before the update call) and its reference-equality CAS rejects a rematerialized
/// instance, so explicit-update persistence cannot be falsified against the fake (DF17/ADR-018). Both the
/// persistence of the update and the conditional/atomic consumed transition (DF15) are Plano 3 acceptance
/// requirements of the EF provider — see the acceptance table in plan-data-storage-matrix.md.
/// </summary>
public abstract class RefreshTokenStoreContractTests : StorageContractTests
{
	// RT-01 + RT-02: a stored refresh token is retrievable by its handle.
	[Fact]
	public async Task Store_ThenGet_ReturnsTheToken()
	{
		await using var harness = await CreateHarnessAsync();
		var store = harness.Storage.GetRefreshTokenStore(harness.RealmA);

		await store.StoreAsync(NewRefreshToken(harness.RealmA, "contract-handle", "subject-a", "client-a"), default);
		var found = await store.GetAsync("contract-handle", default);

		Assert.NotNull(found);
		Assert.Equal("subject-a", found.SubjectId);
		Assert.Equal(harness.RealmA.Id, found.RealmId);
	}

	// RT-02: absent lookup returns null. Load-bearing for the invalid_grant path of the refresh flow;
	// final absence semantics close in Fase 5 (DF25).
	[Fact]
	public async Task Get_UnknownHandle_ReturnsNull()
	{
		await using var harness = await CreateHarnessAsync();

		var found = await harness.Storage.GetRefreshTokenStore(harness.RealmA).GetAsync("contract-unknown", default);

		Assert.Null(found);
	}

	// RT-04: removal makes the token unavailable.
	[Fact]
	public async Task Remove_ThenGet_ReturnsNull()
	{
		await using var harness = await CreateHarnessAsync();
		var store = harness.Storage.GetRefreshTokenStore(harness.RealmA);
		await store.StoreAsync(NewRefreshToken(harness.RealmA, "contract-remove", "subject-a", "client-a"), default);

		await store.RemoveAsync("contract-remove", default);

		Assert.Null(await store.GetAsync("contract-remove", default));
	}

	// RT-05 `preservar` (ADR-017): subject-wide revocation removes all the subject's tokens, reports the
	// count, is idempotent on repetition and leaves other subjects untouched.
	[Fact]
	public async Task RemoveBySubject_RemovesAllTokensOfSubject_ReportsCount_AndIsIdempotent()
	{
		await using var harness = await CreateHarnessAsync();
		var store = harness.Storage.GetRefreshTokenStore(harness.RealmA);

		await store.StoreAsync(NewRefreshToken(harness.RealmA, "handle-s1-a", "subject-1", "client-a"), default);
		await store.StoreAsync(NewRefreshToken(harness.RealmA, "handle-s1-b", "subject-1", "client-b"), default);
		await store.StoreAsync(NewRefreshToken(harness.RealmA, "handle-s2", "subject-2", "client-a"), default);

		var removed = await store.RemoveBySubjectAsync("subject-1", default);
		var removedAgain = await store.RemoveBySubjectAsync("subject-1", default);

		Assert.Equal(2, removed);
		Assert.Equal(0, removedAgain);
		Assert.Null(await store.GetAsync("handle-s1-a", default));
		Assert.Null(await store.GetAsync("handle-s1-b", default));
		Assert.NotNull(await store.GetAsync("handle-s2", default));
	}

	// DF6 + RT-05: subject-wide revocation never crosses realms.
	[Fact]
	public async Task RemoveBySubject_DoesNotCrossRealms()
	{
		await using var harness = await CreateHarnessAsync();
		var storeA = harness.Storage.GetRefreshTokenStore(harness.RealmA);
		var storeB = harness.Storage.GetRefreshTokenStore(harness.RealmB);

		await storeA.StoreAsync(NewRefreshToken(harness.RealmA, "handle-in-a", "subject-x", "client-a"), default);
		await storeB.StoreAsync(NewRefreshToken(harness.RealmB, "handle-in-b", "subject-x", "client-a"), default);

		var removed = await storeA.RemoveBySubjectAsync("subject-x", default);

		Assert.Equal(1, removed);
		Assert.NotNull(await storeB.GetAsync("handle-in-b", default));
	}

	// DF6: the same handle in two realms is two independent records.
	[Fact]
	public async Task SameHandle_InTwoRealms_IsIsolatedPerRealm()
	{
		await using var harness = await CreateHarnessAsync();
		var storeA = harness.Storage.GetRefreshTokenStore(harness.RealmA);
		var storeB = harness.Storage.GetRefreshTokenStore(harness.RealmB);

		await storeA.StoreAsync(NewRefreshToken(harness.RealmA, "contract-shared", "subject-of-a", "client-a"), default);
		await storeB.StoreAsync(NewRefreshToken(harness.RealmB, "contract-shared", "subject-of-b", "client-b"), default);

		await storeA.RemoveAsync("contract-shared", default);

		Assert.Null(await storeA.GetAsync("contract-shared", default));
		var inB = await storeB.GetAsync("contract-shared", default);
		Assert.NotNull(inB);
		Assert.Equal("subject-of-b", inB.SubjectId);
	}

	public sealed class InMemory : RefreshTokenStoreContractTests
	{
		protected override Task<StorageContractHarness> CreateHarnessAsync() => InMemoryStorageHarness.CreateAsync();
	}
}
