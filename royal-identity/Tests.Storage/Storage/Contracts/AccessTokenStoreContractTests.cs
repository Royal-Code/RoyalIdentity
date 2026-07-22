using RoyalIdentity.Models.Tokens;
using Tests.Storage.Support;

namespace Tests.Storage.Contracts;

/// <summary>
/// Contract of <c>IAccessTokenStore</c> (matrix AT-01..AT-04): realm-bound operational store keyed by jti.
/// Fase 5 closed the policies: emission is create-only (duplicate = provider reject, an EF acceptance —
/// not asserted against the fake), reads do not filter logical expiration, comparisons are Ordinal and
/// removals are idempotent.
/// </summary>
public abstract class AccessTokenStoreContractTests : StorageContractTests
{
	// AT-01 + AT-02: a stored token is retrievable by its jti.
	[Fact]
	public async Task Store_ThenGetByJti_ReturnsTheToken()
	{
		await using var harness = await CreateHarnessAsync();
		var store = harness.Storage.GetAccessTokenStore(harness.RealmA);
		var token = NewAccessToken(harness.RealmA, "contract-jti", "client-a");

		await store.StoreAsync(token, default);
		var found = await store.GetAsync("contract-jti", default);

		Assert.NotNull(found);
		Assert.Equal("contract-jti", found.Id);
		Assert.Equal(harness.RealmA.Id, found.RealmId);
	}

	// AT-02 (Fase 5/DF25 closed): absent lookup returns null. Load-bearing for reference-token validation
	// (invalid token path).
	[Fact]
	public async Task Get_UnknownJti_ReturnsNull()
	{
		await using var harness = await CreateHarnessAsync();

		var found = await harness.Storage.GetAccessTokenStore(harness.RealmA).GetAsync("contract-unknown", default);

		Assert.Null(found);
	}

	// DF18 (Fase 5): jti comparison is Ordinal — a jti differing only by casing is another token.
	[Fact]
	public async Task Get_JtiDifferingOnlyByCase_ReturnsNull()
	{
		await using var harness = await CreateHarnessAsync();
		var store = harness.Storage.GetAccessTokenStore(harness.RealmA);
		await store.StoreAsync(NewAccessToken(harness.RealmA, "contract-case-jti", "client-a"), default);

		var found = await store.GetAsync("CONTRACT-CASE-JTI", default);

		Assert.Null(found);
	}

	// AT-02 (Fase 5/DF19): the read does not filter logical expiration — the record is returned and the
	// token validator owns the expiration rule; physical cleanup/TTL is a separate P3 dimension.
	[Fact]
	public async Task Get_ReturnsLogicallyExpiredToken()
	{
		await using var harness = await CreateHarnessAsync();
		var store = harness.Storage.GetAccessTokenStore(harness.RealmA);
		await store.StoreAsync(NewAccessToken(harness.RealmA, "contract-expired", "client-a",
			creationTime: Start.AddHours(-2), lifetime: 60), default);

		var found = await store.GetAsync("contract-expired", default);

		Assert.NotNull(found);
	}

	// AT-03: removal makes the token unavailable.
	[Fact]
	public async Task Remove_ThenGet_ReturnsNull()
	{
		await using var harness = await CreateHarnessAsync();
		var store = harness.Storage.GetAccessTokenStore(harness.RealmA);
		await store.StoreAsync(NewAccessToken(harness.RealmA, "contract-remove", "client-a"), default);

		await store.RemoveAsync("contract-remove", default);
		var found = await store.GetAsync("contract-remove", default);

		Assert.Null(found);
	}

	// AT-03 (Fase 5/DF16/DF25 closed): removing an absent token is an idempotent no-op — revocation is
	// tolerant by rule.
	[Fact]
	public async Task Remove_UnknownJti_CompletesWithoutError()
	{
		await using var harness = await CreateHarnessAsync();

		await harness.Storage.GetAccessTokenStore(harness.RealmA).RemoveAsync("contract-unknown", default);
	}

	// AT-04: reference-token revocation removes only Reference tokens of the exact subject+client pair.
	[Fact]
	public async Task RemoveReferenceTokens_RemovesOnlyReferenceTokensOfSubjectAndClient()
	{
		await using var harness = await CreateHarnessAsync();
		var store = harness.Storage.GetAccessTokenStore(harness.RealmA);

		await store.StoreAsync(NewAccessToken(harness.RealmA, "jti-ref-target", "client-a", "subject-a",
			AccessTokenType.Reference), default);
		await store.StoreAsync(NewAccessToken(harness.RealmA, "jti-jwt-same-pair", "client-a", "subject-a"), default);
		await store.StoreAsync(NewAccessToken(harness.RealmA, "jti-ref-other-subject", "client-a", "subject-b",
			AccessTokenType.Reference), default);
		await store.StoreAsync(NewAccessToken(harness.RealmA, "jti-ref-other-client", "client-b", "subject-a",
			AccessTokenType.Reference), default);
		await store.StoreAsync(NewAccessToken(harness.RealmA, "jti-ref-subject-case", "client-a", "SUBJECT-A",
			AccessTokenType.Reference), default);
		await store.StoreAsync(NewAccessToken(harness.RealmA, "jti-ref-client-case", "CLIENT-A", "subject-a",
			AccessTokenType.Reference), default);

		await store.RemoveReferenceTokensAsync("subject-a", "client-a", default);

		Assert.Null(await store.GetAsync("jti-ref-target", default));
		Assert.NotNull(await store.GetAsync("jti-jwt-same-pair", default));
		Assert.NotNull(await store.GetAsync("jti-ref-other-subject", default));
		Assert.NotNull(await store.GetAsync("jti-ref-other-client", default));
		Assert.NotNull(await store.GetAsync("jti-ref-subject-case", default));
		Assert.NotNull(await store.GetAsync("jti-ref-client-case", default));
	}

	// DF6: the same jti in two realms is two independent records; removing in one realm keeps the other.
	[Fact]
	public async Task SameJti_InTwoRealms_IsIsolatedPerRealm()
	{
		await using var harness = await CreateHarnessAsync();
		var storeA = harness.Storage.GetAccessTokenStore(harness.RealmA);
		var storeB = harness.Storage.GetAccessTokenStore(harness.RealmB);

		await storeA.StoreAsync(NewAccessToken(harness.RealmA, "contract-shared-jti", "client-of-a"), default);
		await storeB.StoreAsync(NewAccessToken(harness.RealmB, "contract-shared-jti", "client-of-b"), default);

		await storeA.RemoveAsync("contract-shared-jti", default);

		Assert.Null(await storeA.GetAsync("contract-shared-jti", default));
		var inB = await storeB.GetAsync("contract-shared-jti", default);
		Assert.NotNull(inB);
		Assert.Equal("client-of-b", inB.ClientId);
	}

	// DF6: a token stored in one realm is not visible from another realm's store.
	[Fact]
	public async Task TokenStoredInOneRealm_IsNotVisibleInAnotherRealm()
	{
		await using var harness = await CreateHarnessAsync();
		await harness.Storage.GetAccessTokenStore(harness.RealmA)
			.StoreAsync(NewAccessToken(harness.RealmA, "contract-only-a", "client-a"), default);

		var inB = await harness.Storage.GetAccessTokenStore(harness.RealmB).GetAsync("contract-only-a", default);

		Assert.Null(inB);
	}

	public sealed class InMemory : AccessTokenStoreContractTests
	{
		protected override Task<StorageContractHarness> CreateHarnessAsync() => InMemoryStorageHarness.CreateAsync();
	}
}
