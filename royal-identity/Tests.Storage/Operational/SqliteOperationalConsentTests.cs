using Microsoft.EntityFrameworkCore;
using RoyalIdentity.Data.Operational.Entities;
using RoyalIdentity.Models;
using Tests.Storage.Operational.Support;

namespace Tests.Storage.Operational;

/// <summary>
/// Acceptances only the EF provider can satisfy for consents (plan Fase 2, DF7/DF14): the identity is the real
/// composite key rather than a concatenated string, the upsert is indivisible per key so concurrent writers
/// never produce two rows, scope casing survives, and expiration is data rather than a filter. The shared
/// semantics live in the provider-neutral <c>UserConsentStoreContractTests</c>.
/// </summary>
public class SqliteOperationalConsentTests
{
    private static readonly DateTime Start = Tests.Storage.Support.StorageContractHarness.Start;

    private static Consent NewConsent(
        Realm realm,
        string subjectId,
        string clientId,
        DateTime? expiration = null,
        params string[] scopes)
    {
        var consent = new Consent
        {
            RealmId = realm.Id,
            SubjectId = subjectId,
            ClientId = clientId,
            CreationTime = Start,
            Expiration = expiration,
        };

        consent.AddScopes(scopes.Select(scope => new ConsentedScope { Scope = scope, CreationTime = Start }));

        return consent;
    }

    private static async Task<List<ConsentEntity>> RowsAsync(SqliteOperationalStorageHarness harness)
    {
        await using var context = harness.NewOperationalContext();

        return await context.Set<ConsentEntity>().AsNoTracking().ToListAsync();
    }

    // CN-01: the upsert makes the last completed write effective, and never creates a second row.
    [Fact]
    public async Task Store_Twice_KeepsOneRow_AndTheLastWriteIsEffective()
    {
        await using var harness = await SqliteOperationalStorageHarness.CreateConcreteAsync();
        var store = harness.Storage.GetUserConsentStore(harness.RealmA);

        await store.StoreUserConsentAsync(NewConsent(harness.RealmA, "subject-a", "client-a", null, "openid"), default);
        await store.StoreUserConsentAsync(
            NewConsent(harness.RealmA, "subject-a", "client-a", Start.AddDays(1), "openid", "profile"), default);

        var row = Assert.Single(await RowsAsync(harness));
        Assert.Equal(Start.AddDays(1), row.ExpiresAtUtc);

        var found = await store.GetUserConsentAsync("subject-a", "client-a", default);
        Assert.NotNull(found);
        Assert.Equal(["openid", "profile"], found.GetValidScopes());
        Assert.Equal(Start.AddDays(1), found.Expiration);
    }

    // CN-01: repeated writers on the same composite key converge on one row — the key constraint is the
    // authority, and the update/insert/update path never inserts a second row. Real multi-connection
    // concurrency belongs to the atomic primitives of MP-2/MP-3 (Fases 4/5); this fixture shares one SQLite
    // connection, so it proves the upsert path rather than a parallel race.
    [Fact]
    public async Task Store_Repeatedly_NeverProducesTwoRows()
    {
        await using var harness = await SqliteOperationalStorageHarness.CreateConcreteAsync();
        var store = harness.Storage.GetUserConsentStore(harness.RealmA);

        var writes = Enumerable.Range(0, 8).Select(index => store.StoreUserConsentAsync(
            NewConsent(harness.RealmA, "subject-race", "client-race", null, $"scope-{index}"), default));
        await Task.WhenAll(writes);

        var row = Assert.Single(await RowsAsync(harness));
        Assert.Equal("subject-race", row.SubjectId);
        Assert.Equal("client-race", row.ClientId);

        var found = await store.GetUserConsentAsync("subject-race", "client-race", default);
        Assert.NotNull(found);
        Assert.Single(found.GetValidScopes());
    }

    // CN-01: the identity is the real composite key. A subject/client pair containing the separator the
    // transitional fake concatenates with must not collide with another pair.
    [Fact]
    public async Task Store_SubjectAndClientContainingASeparator_DoNotCollide()
    {
        await using var harness = await SqliteOperationalStorageHarness.CreateConcreteAsync();
        var store = harness.Storage.GetUserConsentStore(harness.RealmA);

        await store.StoreUserConsentAsync(NewConsent(harness.RealmA, "a.b", "c", null, "first"), default);
        await store.StoreUserConsentAsync(NewConsent(harness.RealmA, "a", "b.c", null, "second"), default);

        Assert.Equal(2, (await RowsAsync(harness)).Count);
        Assert.Equal(["first"], (await store.GetUserConsentAsync("a.b", "c", default))!.GetValidScopes());
        Assert.Equal(["second"], (await store.GetUserConsentAsync("a", "b.c", default))!.GetValidScopes());
    }

    // DF14: scope names compare Ordinal, so two casings are two distinct consented scopes and both survive.
    [Fact]
    public async Task Store_PreservesScopeCasing()
    {
        await using var harness = await SqliteOperationalStorageHarness.CreateConcreteAsync();
        var store = harness.Storage.GetUserConsentStore(harness.RealmA);

        await store.StoreUserConsentAsync(
            NewConsent(harness.RealmA, "subject-a", "client-a", null, "Api.Read", "api.read"), default);

        var found = await store.GetUserConsentAsync("subject-a", "client-a", default);
        Assert.NotNull(found);
        Assert.Equal(["Api.Read", "api.read"], found.GetValidScopes());
    }

    // CN-02/DF17: expiration is persisted data — an expired consent is still readable until cleanup or an
    // explicit removal, and the consent service owns the decision.
    [Fact]
    public async Task Get_ReturnsALogicallyExpiredConsent()
    {
        await using var harness = await SqliteOperationalStorageHarness.CreateConcreteAsync();
        var store = harness.Storage.GetUserConsentStore(harness.RealmA);
        await store.StoreUserConsentAsync(
            NewConsent(harness.RealmA, "subject-a", "client-a", Start.AddHours(-1), "openid"), default);

        var found = await store.GetUserConsentAsync("subject-a", "client-a", default);

        Assert.NotNull(found);
        Assert.Equal(Start.AddHours(-1), found.Expiration);
    }

    // A consent without expiration keeps a null column, which is what makes it ineligible for cleanup (DF17).
    [Fact]
    public async Task Store_WithoutExpiration_KeepsTheColumnNull()
    {
        await using var harness = await SqliteOperationalStorageHarness.CreateConcreteAsync();
        await harness.Storage.GetUserConsentStore(harness.RealmA)
            .StoreUserConsentAsync(NewConsent(harness.RealmA, "subject-a", "client-a", null, "openid"), default);

        Assert.Null(Assert.Single(await RowsAsync(harness)).ExpiresAtUtc);
    }

    // DF5: the same subject/client pair in two realms is two independent consents.
    [Fact]
    public async Task SameSubjectAndClient_InTwoRealms_AreIndependent()
    {
        await using var harness = await SqliteOperationalStorageHarness.CreateConcreteAsync();

        await harness.Storage.GetUserConsentStore(harness.RealmA)
            .StoreUserConsentAsync(NewConsent(harness.RealmA, "subject-a", "client-a", null, "in-a"), default);
        await harness.Storage.GetUserConsentStore(harness.RealmB)
            .StoreUserConsentAsync(NewConsent(harness.RealmB, "subject-a", "client-a", null, "in-b"), default);

        await harness.Storage.GetUserConsentStore(harness.RealmA)
            .RemoveUserConsentAsync("subject-a", "client-a", default);

        Assert.Null(await harness.Storage.GetUserConsentStore(harness.RealmA)
            .GetUserConsentAsync("subject-a", "client-a", default));
        Assert.Equal(["in-b"], (await harness.Storage.GetUserConsentStore(harness.RealmB)
            .GetUserConsentAsync("subject-a", "client-a", default))!.GetValidScopes());
    }

    // DF30: the consented scopes are inside the protected payload, not in a readable column.
    [Fact]
    public async Task Store_KeepsTheScopesInsideTheProtectedPayload()
    {
        await using var harness = await SqliteOperationalStorageHarness.CreateConcreteAsync();
        await harness.Storage.GetUserConsentStore(harness.RealmA)
            .StoreUserConsentAsync(NewConsent(harness.RealmA, "subject-a", "client-a", null, "secret-scope"), default);

        var row = Assert.Single(await RowsAsync(harness));

        Assert.DoesNotContain("secret-scope", row.ProtectedPayload, StringComparison.Ordinal);
        Assert.StartsWith(
            $"v1:{SqliteOperationalStorageHarness.DefaultProtectionProfile}:",
            row.ProtectedPayload,
            StringComparison.Ordinal);
    }

    // DF9: what a read returns is an independent graph.
    [Fact]
    public async Task MutatingAMaterializedConsent_DoesNotPersistImplicitly()
    {
        await using var harness = await SqliteOperationalStorageHarness.CreateConcreteAsync();
        var store = harness.Storage.GetUserConsentStore(harness.RealmA);
        await store.StoreUserConsentAsync(NewConsent(harness.RealmA, "subject-a", "client-a", null, "openid"), default);

        var first = await store.GetUserConsentAsync("subject-a", "client-a", default);
        first!.AddScopes([new ConsentedScope { Scope = "injected", CreationTime = Start }]);

        var second = await store.GetUserConsentAsync("subject-a", "client-a", default);
        Assert.Equal(["openid"], second!.GetValidScopes());
    }

    // A cancelled or failed insert must leave nothing pending in the scope's change tracker. Otherwise the next
    // SaveChanges anywhere in that scope would flush the row and silently complete an operation that did not
    // succeed — the caller having been told it failed.
    [Fact]
    public async Task ACancelledInsert_LeavesNothingPendingInTheScope()
    {
        await using var harness = await SqliteOperationalStorageHarness.CreateConcreteAsync();
        var store = harness.Storage.GetUserConsentStore(harness.RealmA);
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await store.StoreUserConsentAsync(
                NewConsent(harness.RealmA, "subject-cancelled", "client-a", null, "openid"), cancelled.Token));

        // A later, unrelated write in the same scope must not drag the cancelled consent in with it.
        await store.StoreUserConsentAsync(
            NewConsent(harness.RealmA, "subject-other", "client-a", null, "openid"), default);

        Assert.Equal(["subject-other"], (await RowsAsync(harness)).Select(row => row.SubjectId));
        Assert.Null(await store.GetUserConsentAsync("subject-cancelled", "client-a", default));
    }

    // DF19: a pre-cancelled token stops the operation before it reaches the provider.
    [Fact]
    public async Task PreCancelledToken_IsPropagatedToTheProvider()
    {
        await using var harness = await SqliteOperationalStorageHarness.CreateConcreteAsync();
        var store = harness.Storage.GetUserConsentStore(harness.RealmA);
        await store.StoreUserConsentAsync(NewConsent(harness.RealmA, "subject-a", "client-a", null, "openid"), default);
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await store.GetUserConsentAsync("subject-a", "client-a", cancelled.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await store.RemoveUserConsentAsync("subject-a", "client-a", cancelled.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await store.StoreUserConsentAsync(
                NewConsent(harness.RealmA, "subject-b", "client-b", null, "openid"), cancelled.Token));
    }
}
