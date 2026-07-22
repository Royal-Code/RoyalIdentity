using Tests.Storage.Support;

namespace Tests.Storage.Contracts;

/// <summary>
/// Contract of <c>IRefreshTokenStore</c> (matrix RT-01..RT-05). The update scenario asserts only explicit
/// persistence (DF17) — the current get→mutate→update pattern (RT-03) is classified `substituir` and the
/// conditional/atomic transition under concurrency is a Plano 3 acceptance requirement (DF15), not asserted
/// against the transitional fake.
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

	// RT-03 (explicit persistence only — DF17): an explicit update persists the consumed transition so a
	// later read observes it. The atomic compare-and-swap semantics are a Plano 3 acceptance (DF15).
	[Fact]
	public async Task Update_PersistsConsumedTime()
	{
		await using var harness = await CreateHarnessAsync();
		var store = harness.Storage.GetRefreshTokenStore(harness.RealmA);
		await store.StoreAsync(NewRefreshToken(harness.RealmA, "contract-consume", "subject-a", "client-a"), default);

		var token = await store.GetAsync("contract-consume", default);
		Assert.NotNull(token);
		token.ConsumedTime = Start.AddMinutes(5);
		await store.UpdateAsync(token, default);

		var reloaded = await store.GetAsync("contract-consume", default);

		Assert.NotNull(reloaded);
		Assert.Equal(Start.AddMinutes(5), reloaded.ConsumedTime);
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
