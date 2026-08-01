using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using RoyalIdentity.Contracts.Storage;
using RoyalIdentity.Data.Operational.Entities;
using RoyalIdentity.Models;
using RoyalIdentity.Models.Scopes;
using RoyalIdentity.Models.Tokens;
using RoyalIdentity.Storage.EntityFramework.Operational.Materialization;
using Tests.Storage.Operational.Support;

namespace Tests.Storage.Operational;

/// <summary>
/// Acceptances only the EF provider can satisfy for authorization codes (plan Fase 4, DF11/DF38): the
/// single-use consumption of MP-2, its binding condition, and the fact that neither a mismatched binding nor a
/// later pipeline failure leaves the code reusable. The shared storage semantics live in the provider-neutral
/// <c>AuthorizationCodeStoreContractTests</c>.
/// </summary>
public class SqliteOperationalAuthorizationCodeTests
{
    private static readonly OperationalLookupDigest Digest = new();
    private static readonly DateTime Start = Tests.Storage.Support.StorageContractHarness.Start;

    private const string ClientId = "client-a";
    private const string RedirectUri = "https://client.contract.test/callback";

    private static AuthorizationCode NewCode(
        Realm realm,
        string clientId = ClientId,
        string redirectUri = RedirectUri,
        string subjectId = "subject-a",
        int lifetime = 300)
    {
        var subject = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", subjectId)], "contract"));

        return new AuthorizationCode(clientId, subject, Start, lifetime,
            new RequestedResources(), redirectUri)
        {
            RealmId = realm.Id,
            SessionId = "session-a",
        };
    }

    private static async Task<List<ProtocolArtifactEntity>> ArtifactsAsync(SqliteOperationalStorageHarness harness)
    {
        await using var context = harness.NewOperationalContext();

        return await context.Set<ProtocolArtifactEntity>()
            .AsNoTracking()
            .Where(artifact => artifact.ArtifactType == ProtocolArtifactTypes.AuthorizationCode)
            .ToListAsync();
    }

    private static IAuthorizationCodeStore SingleUse(SqliteOperationalStorageHarness harness, Realm realm)
        => harness.Storage.GetAuthorizationCodeStore(realm);

    // AC-01: create-only. A duplicate handle in the same realm fails visibly instead of overwriting a live code.
    [Fact]
    public async Task Store_DuplicateHandleInTheSameRealm_FailsWithoutOverwriting()
    {
        await using var harness = await SqliteOperationalStorageHarness.CreateConcreteAsync();
        var store = harness.Storage.GetAuthorizationCodeStore(harness.RealmA);
        var code = NewCode(harness.RealmA);
        await store.StoreAuthorizationCodeAsync(code, default);

        var duplicate = NewCode(harness.RealmA, clientId: "client-other");
        typeof(AuthorizationCode).GetProperty(nameof(AuthorizationCode.Code))!.GetValue(duplicate);

        await Assert.ThrowsAnyAsync<DbUpdateException>(async () =>
            await store.StoreAuthorizationCodeAsync(
                new AuthorizationCode(
                    code.Code, "client-other", duplicate.Subject, Start, 300,
                    new RequestedResources(), RedirectUri)
                {
                    RealmId = harness.RealmA.Id,
                },
                default));

        var found = await store.GetAuthorizationCodeAsync(code.Code, default);
        Assert.NotNull(found);
        Assert.Equal(ClientId, found.ClientId);
    }

    // DF38: the row is keyed by the digest of the handle; the raw code never reaches a column or the payload.
    [Fact]
    public async Task Store_KeepsTheRawCodeOutOfEveryColumnAndOutOfThePayload()
    {
        await using var harness = await SqliteOperationalStorageHarness.CreateConcreteAsync();
        var code = NewCode(harness.RealmA);

        await harness.Storage.GetAuthorizationCodeStore(harness.RealmA).StoreAuthorizationCodeAsync(code, default);

        var row = Assert.Single(await ArtifactsAsync(harness));
        Assert.Equal(Digest.Compute(OperationalRecordTypes.AuthorizationCode, code.Code), row.LookupDigest);
        Assert.DoesNotContain(code.Code, row.LookupDigest, StringComparison.Ordinal);
        Assert.DoesNotContain(code.Code, row.ProtectedPayload!, StringComparison.Ordinal);
        // The binding lives in queryable columns precisely because the consumption condition evaluates it.
        Assert.Equal(ClientId, row.ClientId);
        Assert.Equal(RedirectUri, row.RedirectUri);
    }

    // MP-2: a matching consumption returns the code once and removes it.
    [Fact]
    public async Task Consume_WithTheExpectedBinding_ReturnsTheCodeAndRemovesIt()
    {
        await using var harness = await SqliteOperationalStorageHarness.CreateConcreteAsync();
        var store = harness.Storage.GetAuthorizationCodeStore(harness.RealmA);
        var code = NewCode(harness.RealmA);
        await store.StoreAuthorizationCodeAsync(code, default);

        var consumed = await SingleUse(harness, harness.RealmA)
            .ConsumeAuthorizationCodeAsync(code.Code, ClientId, RedirectUri, default);

        Assert.NotNull(consumed);
        Assert.Equal(code.Code, consumed.Code);
        Assert.Equal(ClientId, consumed.ClientId);
        Assert.Equal(RedirectUri, consumed.RedirectUri);
        Assert.Equal("subject-a", consumed.Subject.FindFirst("sub")!.Value);
        Assert.Empty(await ArtifactsAsync(harness));
    }

    // DF11: a second consumption of the same code yields nothing.
    [Fact]
    public async Task Consume_Twice_SucceedsOnlyTheFirstTime()
    {
        await using var harness = await SqliteOperationalStorageHarness.CreateConcreteAsync();
        var store = harness.Storage.GetAuthorizationCodeStore(harness.RealmA);
        var code = NewCode(harness.RealmA);
        await store.StoreAuthorizationCodeAsync(code, default);
        var singleUse = SingleUse(harness, harness.RealmA);

        Assert.NotNull(await singleUse.ConsumeAuthorizationCodeAsync(code.Code, ClientId, RedirectUri, default));
        Assert.Null(await singleUse.ConsumeAuthorizationCodeAsync(code.Code, ClientId, RedirectUri, default));
    }

    // DF11: a mismatched binding returns the same null as an absent code — and, crucially, does not consume it,
    // so an invalid request cannot deny the legitimate one.
    [Theory]
    [InlineData("client-other", RedirectUri)]
    [InlineData(ClientId, "https://attacker.example/callback")]
    [InlineData("CLIENT-A", RedirectUri)]
    [InlineData(ClientId, "HTTPS://CLIENT.CONTRACT.TEST/CALLBACK")]
    public async Task Consume_WithAMismatchedBinding_ReturnsNullWithoutConsuming(
        string clientId, string redirectUri)
    {
        await using var harness = await SqliteOperationalStorageHarness.CreateConcreteAsync();
        var store = harness.Storage.GetAuthorizationCodeStore(harness.RealmA);
        var code = NewCode(harness.RealmA);
        await store.StoreAuthorizationCodeAsync(code, default);

        var consumed = await SingleUse(harness, harness.RealmA)
            .ConsumeAuthorizationCodeAsync(code.Code, clientId, redirectUri, default);

        Assert.Null(consumed);
        Assert.Single(await ArtifactsAsync(harness));

        // The legitimate request still works afterwards.
        Assert.NotNull(await SingleUse(harness, harness.RealmA)
            .ConsumeAuthorizationCodeAsync(code.Code, ClientId, RedirectUri, default));
    }

    // DF11: an unknown handle is null, with nothing removed.
    [Fact]
    public async Task Consume_UnknownHandle_ReturnsNull()
    {
        await using var harness = await SqliteOperationalStorageHarness.CreateConcreteAsync();

        var consumed = await SingleUse(harness, harness.RealmA)
            .ConsumeAuthorizationCodeAsync("never-issued", ClientId, RedirectUri, default);

        Assert.Null(consumed);
    }

    // DF11: expiration is not part of the condition. An expired code is still consumed — the pipeline rejects
    // it afterwards — so a losing attempt can never retry it.
    [Fact]
    public async Task Consume_AnExpiredCode_StillConsumesIt()
    {
        await using var harness = await SqliteOperationalStorageHarness.CreateConcreteAsync();
        var store = harness.Storage.GetAuthorizationCodeStore(harness.RealmA);
        var code = NewCode(harness.RealmA, lifetime: 1);
        await store.StoreAuthorizationCodeAsync(code, default);
        harness.Clock.Advance(TimeSpan.FromHours(1));

        var consumed = await SingleUse(harness, harness.RealmA)
            .ConsumeAuthorizationCodeAsync(code.Code, ClientId, RedirectUri, default);

        Assert.NotNull(consumed);
        Assert.Equal(1, consumed.Lifetime);
        Assert.Empty(await ArtifactsAsync(harness));
    }

    // DF5: the same handle in two realms is two codes, and consuming in one realm leaves the other untouched.
    [Fact]
    public async Task Consume_NeverCrossesRealms()
    {
        await using var harness = await SqliteOperationalStorageHarness.CreateConcreteAsync();
        var inA = NewCode(harness.RealmA);
        await harness.Storage.GetAuthorizationCodeStore(harness.RealmA).StoreAuthorizationCodeAsync(inA, default);

        var consumed = await SingleUse(harness, harness.RealmB)
            .ConsumeAuthorizationCodeAsync(inA.Code, ClientId, RedirectUri, default);

        Assert.Null(consumed);
        Assert.NotNull(await harness.Storage.GetAuthorizationCodeStore(harness.RealmA)
            .GetAuthorizationCodeAsync(inA.Code, default));
    }

    // AC-03: the administrative removal is unchanged and stays idempotent.
    [Fact]
    public async Task Remove_IsIdempotent_AndIndependentOfTheBinding()
    {
        await using var harness = await SqliteOperationalStorageHarness.CreateConcreteAsync();
        var store = harness.Storage.GetAuthorizationCodeStore(harness.RealmA);
        var code = NewCode(harness.RealmA);
        await store.StoreAuthorizationCodeAsync(code, default);

        await store.RemoveAuthorizationCodeAsync(code.Code, default);
        await store.RemoveAuthorizationCodeAsync(code.Code, default);
        await store.RemoveAuthorizationCodeAsync("never-issued", default);

        Assert.Empty(await ArtifactsAsync(harness));
    }

    // DF9: the consumed code is a complete, independent graph — the flow gets everything it needs from it.
    [Fact]
    public async Task Consume_ReturnsTheCompleteGraph()
    {
        await using var harness = await SqliteOperationalStorageHarness.CreateConcreteAsync();
        var code = NewCode(harness.RealmA);
        code.Nonce = "nonce-value";
        code.StateHash = "state-hash";
        code.CodeChallenge = "challenge";
        code.CodeChallengeMethod = "S256";
        code.Properties = new Dictionary<string, string> { ["custom"] = "value" };
        await harness.Storage.GetAuthorizationCodeStore(harness.RealmA).StoreAuthorizationCodeAsync(code, default);

        var consumed = await SingleUse(harness, harness.RealmA)
            .ConsumeAuthorizationCodeAsync(code.Code, ClientId, RedirectUri, default);

        Assert.NotNull(consumed);
        Assert.Equal("nonce-value", consumed.Nonce);
        Assert.Equal("state-hash", consumed.StateHash);
        Assert.Equal("challenge", consumed.CodeChallenge);
        Assert.Equal("S256", consumed.CodeChallengeMethod);
        Assert.Equal("session-a", consumed.SessionId);
        Assert.Equal("value", consumed.Properties!["custom"]);
        Assert.Equal(harness.RealmA.Id, consumed.RealmId);
    }

    // DF19: a pre-cancelled token stops the operation before it reaches the provider.
    [Fact]
    public async Task PreCancelledToken_IsPropagatedToTheProvider()
    {
        await using var harness = await SqliteOperationalStorageHarness.CreateConcreteAsync();
        var store = harness.Storage.GetAuthorizationCodeStore(harness.RealmA);
        var code = NewCode(harness.RealmA);
        await store.StoreAuthorizationCodeAsync(code, default);
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await store.GetAuthorizationCodeAsync(code.Code, cancelled.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await store.RemoveAuthorizationCodeAsync(code.Code, cancelled.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await SingleUse(harness, harness.RealmA)
                .ConsumeAuthorizationCodeAsync(code.Code, ClientId, RedirectUri, cancelled.Token));
    }
}
