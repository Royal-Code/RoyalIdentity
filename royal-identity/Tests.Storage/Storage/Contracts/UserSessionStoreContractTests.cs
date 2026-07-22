using Tests.Storage.Support;

namespace Tests.Storage.Contracts;

/// <summary>
/// Contract of <c>IUserSessionStore</c> (matrix SS-01..SS-06): pure, realm-bound session persistence
/// (ADR-014) with explicit end/touch and idempotent subject-wide revocation (ADR-017).
/// </summary>
public abstract class UserSessionStoreContractTests : StorageContractTests
{
	// SS-01 + SS-02: a created session is retrievable by sid.
	[Fact]
	public async Task Create_ThenFindById_ReturnsTheSession()
	{
		await using var harness = await CreateHarnessAsync();
		var store = harness.Storage.GetUserSessionStore(harness.RealmA);

		await store.CreateAsync(NewSession("contract-sid", "subject-a"));
		var found = await store.FindByIdAsync("contract-sid");

		Assert.NotNull(found);
		Assert.Equal("subject-a", found.SubjectId);
		Assert.True(found.IsActive);
	}

	// SS-02: absent sid returns null (`preservar` — ADR-014 pure lookup; final absence semantics DF25/Fase 5).
	[Fact]
	public async Task FindById_UnknownSid_ReturnsNull()
	{
		await using var harness = await CreateHarnessAsync();

		var found = await harness.Storage.GetUserSessionStore(harness.RealmA).FindByIdAsync("contract-unknown");

		Assert.Null(found);
	}

	// SS-03 `preservar` (ADR-014): recording the same client twice keeps a single deduplicated entry,
	// preserves FirstSeenAt and refreshes LastSeenAt by the store clock.
	[Fact]
	public async Task RecordClient_SameClientTwice_KeepsSingleEntry_PreservesFirstSeenAt_AndRefreshesLastSeenAt()
	{
		await using var harness = await CreateHarnessAsync();
		var store = harness.Storage.GetUserSessionStore(harness.RealmA);
		await store.CreateAsync(NewSession("contract-sid", "subject-a"));

		var firstSeen = harness.Clock.GetUtcNow().UtcDateTime;
		await store.RecordClientAsync("contract-sid", "client-a");

		harness.Clock.Advance(TimeSpan.FromMinutes(10));
		var lastSeen = harness.Clock.GetUtcNow().UtcDateTime;
		await store.RecordClientAsync("contract-sid", "client-a");

		var session = await store.FindByIdAsync("contract-sid");

		Assert.NotNull(session);
		var entry = Assert.Single(session.Clients);
		Assert.Equal("client-a", entry.ClientId);
		Assert.Equal(firstSeen, entry.FirstSeenAt);
		Assert.Equal(lastSeen, entry.LastSeenAt);
	}

	// SS-03: recording a client on an absent session completes without error (no-op today; DF25/Fase 5).
	[Fact]
	public async Task RecordClient_UnknownSession_CompletesWithoutError()
	{
		await using var harness = await CreateHarnessAsync();

		await harness.Storage.GetUserSessionStore(harness.RealmA).RecordClientAsync("contract-unknown", "client-a");
	}

	// SS-04 `preservar` (ADR-014/017): ending a session marks it inactive, observable by a later read.
	[Fact]
	public async Task End_MarksSessionInactive()
	{
		await using var harness = await CreateHarnessAsync();
		var store = harness.Storage.GetUserSessionStore(harness.RealmA);
		await store.CreateAsync(NewSession("contract-sid", "subject-a"));

		var ended = await store.EndAsync("contract-sid");
		var reloaded = await store.FindByIdAsync("contract-sid");

		Assert.NotNull(ended);
		Assert.NotNull(reloaded);
		Assert.False(reloaded.IsActive);
	}

	// SS-04: ending an absent session returns null.
	[Fact]
	public async Task End_UnknownSid_ReturnsNull()
	{
		await using var harness = await CreateHarnessAsync();

		var ended = await harness.Storage.GetUserSessionStore(harness.RealmA).EndAsync("contract-unknown");

		Assert.Null(ended);
	}

	// SS-05 `preservar` (ADR-017): the idle touch persists both timestamps supplied by the caller.
	[Fact]
	public async Task Touch_PersistsLastSeenAtAndExpiresAt()
	{
		await using var harness = await CreateHarnessAsync();
		var store = harness.Storage.GetUserSessionStore(harness.RealmA);
		await store.CreateAsync(NewSession("contract-sid", "subject-a"));

		var lastSeen = Start.AddMinutes(30);
		var expiresAt = Start.AddHours(8);
		await store.TouchAsync("contract-sid", lastSeen, expiresAt);

		var session = await store.FindByIdAsync("contract-sid");

		Assert.NotNull(session);
		Assert.Equal(lastSeen, session.LastSeenAt);
		Assert.Equal(expiresAt, session.ExpiresAt);
	}

	// SS-05: touching an absent session completes without error (no-op today; DF25/Fase 5).
	[Fact]
	public async Task Touch_UnknownSid_CompletesWithoutError()
	{
		await using var harness = await CreateHarnessAsync();

		await harness.Storage.GetUserSessionStore(harness.RealmA)
			.TouchAsync("contract-unknown", Start, null);
	}

	// SS-06 `preservar` (ADR-017): subject-wide revocation ends only the subject's active sessions,
	// preserves the excepted sid, reports the count and is idempotent.
	[Fact]
	public async Task EndSessionsForSubject_EndsActiveSessionsExceptGivenSid_ReportsCount_AndIsIdempotent()
	{
		await using var harness = await CreateHarnessAsync();
		var store = harness.Storage.GetUserSessionStore(harness.RealmA);

		await store.CreateAsync(NewSession("sid-current", "subject-1"));
		await store.CreateAsync(NewSession("sid-other-1", "subject-1"));
		await store.CreateAsync(NewSession("sid-other-2", "subject-1"));
		await store.CreateAsync(NewSession("sid-already-ended", "subject-1", isActive: false));
		await store.CreateAsync(NewSession("sid-other-subject", "subject-2"));

		var ended = await store.EndSessionsForSubjectAsync("subject-1", "sid-current");
		var endedAgain = await store.EndSessionsForSubjectAsync("subject-1", "sid-current");

		Assert.Equal(2, ended);
		Assert.Equal(0, endedAgain);
		Assert.True((await store.FindByIdAsync("sid-current"))!.IsActive);
		Assert.False((await store.FindByIdAsync("sid-other-1"))!.IsActive);
		Assert.False((await store.FindByIdAsync("sid-other-2"))!.IsActive);
		Assert.True((await store.FindByIdAsync("sid-other-subject"))!.IsActive);
	}

	// DF6: the same sid in two realms is two independent sessions; ending in one realm keeps the other active.
	[Fact]
	public async Task SameSid_InTwoRealms_IsIsolatedPerRealm()
	{
		await using var harness = await CreateHarnessAsync();
		var storeA = harness.Storage.GetUserSessionStore(harness.RealmA);
		var storeB = harness.Storage.GetUserSessionStore(harness.RealmB);

		await storeA.CreateAsync(NewSession("contract-shared-sid", "subject-of-a"));
		await storeB.CreateAsync(NewSession("contract-shared-sid", "subject-of-b"));

		await storeA.EndAsync("contract-shared-sid");

		var inA = await storeA.FindByIdAsync("contract-shared-sid");
		var inB = await storeB.FindByIdAsync("contract-shared-sid");

		Assert.NotNull(inA);
		Assert.False(inA.IsActive);
		Assert.NotNull(inB);
		Assert.True(inB.IsActive);
		Assert.Equal("subject-of-b", inB.SubjectId);
	}

	// DF6: a session created in one realm is not found in another realm's store.
	[Fact]
	public async Task SessionCreatedInOneRealm_IsNotVisibleInAnotherRealm()
	{
		await using var harness = await CreateHarnessAsync();
		await harness.Storage.GetUserSessionStore(harness.RealmA).CreateAsync(NewSession("contract-only-a", "subject-a"));

		var inB = await harness.Storage.GetUserSessionStore(harness.RealmB).FindByIdAsync("contract-only-a");

		Assert.Null(inB);
	}

	public sealed class InMemory : UserSessionStoreContractTests
	{
		protected override Task<StorageContractHarness> CreateHarnessAsync() => InMemoryStorageHarness.CreateAsync();
	}
}
