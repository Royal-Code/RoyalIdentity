using Tests.Storage.Support;

namespace Tests.Storage.Contracts;

/// <summary>
/// Contract of <c>IUserConsentStore</c> (matrix CN-01..CN-03): consent identity is realm+subject+client
/// (`preservar` — product). Key-encoding details of the fake (subject+"."+client concatenation) are backing
/// accidents and are not asserted; the final encoding/upsert policy closes in Fase 5 (DF16/DF18).
/// </summary>
public abstract class UserConsentStoreContractTests : StorageContractTests
{
	// CN-01 + CN-02: a stored consent is retrievable by subject+client.
	[Fact]
	public async Task Store_ThenGet_ReturnsConsentForSubjectAndClient()
	{
		await using var harness = await CreateHarnessAsync();
		var store = harness.Storage.GetUserConsentStore(harness.RealmA);

		await store.StoreUserConsentAsync(NewConsent(harness.RealmA, "subject-a", "client-a", "openid"), default);
		var found = await store.GetUserConsentAsync("subject-a", "client-a", default);

		Assert.NotNull(found);
		Assert.Equal("subject-a", found.SubjectId);
		Assert.Equal("client-a", found.ClientId);
		Assert.Contains("openid", found.GetValidScopes());
	}

	// CN-02: absent pair returns null. Load-bearing for the consent flow (no consent → consent screen);
	// final absence semantics close in Fase 5 (DF25).
	[Fact]
	public async Task Get_UnknownSubjectClientPair_ReturnsNull()
	{
		await using var harness = await CreateHarnessAsync();
		var store = harness.Storage.GetUserConsentStore(harness.RealmA);
		await store.StoreUserConsentAsync(NewConsent(harness.RealmA, "subject-a", "client-a", "openid"), default);

		Assert.Null(await store.GetUserConsentAsync("subject-a", "client-other", default));
		Assert.Null(await store.GetUserConsentAsync("subject-other", "client-a", default));
	}

	// CN-01: storing again for the same subject+client makes the latest consent the effective one
	// (DefaultConsentService updates consent this way; formal upsert-vs-replace policy — Fase 5, DF16).
	[Fact]
	public async Task Store_SameSubjectAndClient_MakesLatestConsentEffective()
	{
		await using var harness = await CreateHarnessAsync();
		var store = harness.Storage.GetUserConsentStore(harness.RealmA);

		await store.StoreUserConsentAsync(NewConsent(harness.RealmA, "subject-a", "client-a", "openid"), default);
		await store.StoreUserConsentAsync(
			NewConsent(harness.RealmA, "subject-a", "client-a", "openid", "profile"), default);

		var found = await store.GetUserConsentAsync("subject-a", "client-a", default);

		Assert.NotNull(found);
		Assert.Contains("profile", found.GetValidScopes());
	}

	// CN-03: removal makes the consent unavailable; removing an absent pair completes without error.
	[Fact]
	public async Task Remove_ThenGet_ReturnsNull_AndRemovingAbsentCompletes()
	{
		await using var harness = await CreateHarnessAsync();
		var store = harness.Storage.GetUserConsentStore(harness.RealmA);
		await store.StoreUserConsentAsync(NewConsent(harness.RealmA, "subject-a", "client-a", "openid"), default);

		await store.RemoveUserConsentAsync("subject-a", "client-a", default);
		await store.RemoveUserConsentAsync("subject-a", "client-a", default);

		Assert.Null(await store.GetUserConsentAsync("subject-a", "client-a", default));
	}

	// DF6 + invariante 6: the same subject+client pair in two realms is two independent consents;
	// removal in one realm keeps the other.
	[Fact]
	public async Task SameSubjectAndClient_InTwoRealms_IsIsolatedPerRealm()
	{
		await using var harness = await CreateHarnessAsync();
		var storeA = harness.Storage.GetUserConsentStore(harness.RealmA);
		var storeB = harness.Storage.GetUserConsentStore(harness.RealmB);

		await storeA.StoreUserConsentAsync(NewConsent(harness.RealmA, "subject-x", "client-x", "openid"), default);
		await storeB.StoreUserConsentAsync(
			NewConsent(harness.RealmB, "subject-x", "client-x", "openid", "profile"), default);

		await storeA.RemoveUserConsentAsync("subject-x", "client-x", default);

		Assert.Null(await storeA.GetUserConsentAsync("subject-x", "client-x", default));
		var inB = await storeB.GetUserConsentAsync("subject-x", "client-x", default);
		Assert.NotNull(inB);
		Assert.Contains("profile", inB.GetValidScopes());
	}

	public sealed class InMemory : UserConsentStoreContractTests
	{
		protected override Task<StorageContractHarness> CreateHarnessAsync() => InMemoryStorageHarness.CreateAsync();
	}
}
