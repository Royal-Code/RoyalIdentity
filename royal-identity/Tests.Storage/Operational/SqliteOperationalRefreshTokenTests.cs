using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using RoyalIdentity.Contracts.Storage;
using RoyalIdentity.Data.Operational.Entities;
using RoyalIdentity.Models;
using RoyalIdentity.Models.Tokens;
using RoyalIdentity.Options;
using RoyalIdentity.Storage.EntityFramework.Operational.Materialization;
using Tests.Storage.Operational.Support;

namespace Tests.Storage.Operational;

/// <summary>
/// Acceptances only the EF provider can satisfy for refresh tokens (plan Fase 5, DF12/DF32/DF41): the
/// conditional transition of MP-3, the state version that makes a lost race observable, the claims mode
/// captured at issuance, and the absence of any dependency on the access token issued alongside.
/// </summary>
public class SqliteOperationalRefreshTokenTests
{
    private static readonly OperationalLookupDigest Digest = new();
    private static readonly DateTime Start = Tests.Storage.Support.StorageContractHarness.Start;

    private static RefreshToken NewToken(
        Realm realm,
        string handle = "rt-handle",
        string subjectId = "subject-a",
        string clientId = "client-a",
        RefreshTokenClaimsMode claimsMode = RefreshTokenClaimsMode.Current,
        int lifetime = 3600)
    {
        var token = new RefreshToken(
            subjectId, "session-a", ["openid", "api.read"], clientId, "https://issuer.contract.test",
            Start, lifetime, handle)
        {
            RealmId = realm.Id,
            ClaimsMode = claimsMode,
        };

        token.ResourceUris.Add("https://api.example/orders");
        if (claimsMode is RefreshTokenClaimsMode.Snapshot)
            token.IdentityTokenClaims.Add(new Claim("website", "https://snapshot.example"));

        return token;
    }

    private static async Task<List<ProtocolArtifactEntity>> ArtifactsAsync(SqliteOperationalStorageHarness harness)
    {
        await using var context = harness.NewOperationalContext();

        return await context.Set<ProtocolArtifactEntity>()
            .AsNoTracking()
            .Where(artifact => artifact.ArtifactType == ProtocolArtifactTypes.RefreshToken)
            .ToListAsync();
    }

    private static IRefreshTokenStore Versioned(SqliteOperationalStorageHarness harness, Realm realm)
        => harness.Storage.GetRefreshTokenStore(realm);

    // RT-01: create-only. A duplicate handle in the same realm fails visibly.
    [Fact]
    public async Task Store_DuplicateHandleInTheSameRealm_FailsWithoutOverwriting()
    {
        await using var harness = await SqliteOperationalStorageHarness.CreateConcreteAsync();
        var store = harness.Storage.GetRefreshTokenStore(harness.RealmA);
        await store.StoreAsync(NewToken(harness.RealmA, subjectId: "subject-first"), default);

        await Assert.ThrowsAnyAsync<DbUpdateException>(
            async () => await store.StoreAsync(NewToken(harness.RealmA, subjectId: "subject-second"), default));

        Assert.Equal("subject-first", (await store.GetAsync("rt-handle", default))!.SubjectId);
    }

    // DF38: the row is keyed by the digest of the handle; the raw handle never reaches a column or the payload.
    [Fact]
    public async Task Store_KeepsTheRawHandleOutOfEveryColumnAndOutOfThePayload()
    {
        await using var harness = await SqliteOperationalStorageHarness.CreateConcreteAsync();
        await harness.Storage.GetRefreshTokenStore(harness.RealmA)
            .StoreAsync(NewToken(harness.RealmA, "secret-handle"), default);

        var row = Assert.Single(await ArtifactsAsync(harness));

        Assert.Equal(Digest.Compute(OperationalRecordTypes.RefreshToken, "secret-handle"), row.LookupDigest);
        Assert.DoesNotContain("secret-handle", row.LookupDigest, StringComparison.Ordinal);
        Assert.DoesNotContain("secret-handle", row.ProtectedPayload!, StringComparison.Ordinal);
    }

    // DF41: nothing identifies the access token issued alongside — not a column, not a claim in the payload.
    [Fact]
    public async Task Store_PersistsNoIdentifierOfAnyAccessToken()
    {
        await using var harness = await SqliteOperationalStorageHarness.CreateConcreteAsync();
        var store = harness.Storage.GetRefreshTokenStore(harness.RealmA);
        await store.StoreAsync(NewToken(harness.RealmA), default);

        var restored = await store.GetAsync("rt-handle", default);

        Assert.NotNull(restored);
        Assert.DoesNotContain(restored.Claims, claim => claim.Type == "jti");
        Assert.Equal("subject-a", restored.SubjectId);
        Assert.Equal("session-a", restored.SessionId);
        Assert.Equal(["openid", "api.read"], restored.RequestedScopes);
        Assert.Equal(["https://api.example/orders"], restored.ResourceUris);
    }

    // DF32: the mode is captured at issuance, so a later realm change never reinterprets an existing token.
    [Theory]
    [InlineData(RefreshTokenClaimsMode.Current)]
    [InlineData(RefreshTokenClaimsMode.Snapshot)]
    public async Task Store_CapturesTheClaimsMode_AndARealmChangeDoesNotReinterpretIt(RefreshTokenClaimsMode mode)
    {
        await using var harness = await SqliteOperationalStorageHarness.CreateConcreteAsync();
        var realm = harness.RealmA;
        var store = harness.Storage.GetRefreshTokenStore(realm);
        await store.StoreAsync(NewToken(realm, claimsMode: mode), default);

        realm.Options.RefreshTokens.ClaimsMode = mode is RefreshTokenClaimsMode.Current
            ? RefreshTokenClaimsMode.Snapshot
            : RefreshTokenClaimsMode.Current;

        var restored = (await harness.Storage.GetRefreshTokenStore(realm).GetAsync("rt-handle", default))!;

        Assert.Equal(mode, restored.ClaimsMode);
        if (mode is RefreshTokenClaimsMode.Snapshot)
        {
            Assert.Equal(
                "https://snapshot.example",
                Assert.Single(restored.IdentityTokenClaims, claim => claim.Type == "website").Value);
        }
        else
        {
            Assert.Empty(restored.IdentityTokenClaims);
        }
    }

    // DF12: the first transition succeeds and is observable; the state version moves with it.
    [Fact]
    public async Task TryConsume_FirstTransition_SucceedsAndBumpsTheVersion()
    {
        await using var harness = await SqliteOperationalStorageHarness.CreateConcreteAsync();
        var store = harness.Storage.GetRefreshTokenStore(harness.RealmA);
        await store.StoreAsync(NewToken(harness.RealmA), default);
        var materialized = await store.GetAsync("rt-handle", default);

        var transition = await Versioned(harness, harness.RealmA)
            .TryConsumeAsync("rt-handle", materialized!.StateVersion, Start.AddMinutes(1), default);

        Assert.Equal(RefreshTokenTransitionOutcome.Succeeded, transition.Outcome);
        Assert.NotNull(transition.Current);
        Assert.Equal(Start.AddMinutes(1), transition.Current.ConsumedTime);
        Assert.NotEqual(materialized.StateVersion, transition.Current.StateVersion);
    }

    // DF12: a second transition at the same expected version is AlreadyConsumed — never a silent success — and
    // it reports the rematerialized state, the only thing the tolerance policy may look at.
    [Fact]
    public async Task TryConsume_Twice_ReportsAlreadyConsumed_WithTheRematerializedState()
    {
        await using var harness = await SqliteOperationalStorageHarness.CreateConcreteAsync();
        var store = harness.Storage.GetRefreshTokenStore(harness.RealmA);
        await store.StoreAsync(NewToken(harness.RealmA), default);
        var materialized = await store.GetAsync("rt-handle", default);
        var versioned = Versioned(harness, harness.RealmA);

        await versioned.TryConsumeAsync("rt-handle", materialized!.StateVersion, Start.AddMinutes(1), default);
        var second = await versioned.TryConsumeAsync("rt-handle", materialized.StateVersion, Start.AddMinutes(2), default);

        Assert.Equal(RefreshTokenTransitionOutcome.AlreadyConsumed, second.Outcome);
        Assert.False(second.IsSuccess);
        Assert.NotNull(second.Current);
        // The first consumption's timestamp survives: the second attempt did not overwrite it.
        Assert.Equal(Start.AddMinutes(1), second.Current.ConsumedTime);
    }

    // The falsifiable form of the CAS: a stale expected version never wins, even on a token nobody consumed.
    [Fact]
    public async Task TryConsume_WithAStaleVersion_IsAConflict()
    {
        await using var harness = await SqliteOperationalStorageHarness.CreateConcreteAsync();
        var store = harness.Storage.GetRefreshTokenStore(harness.RealmA);
        var token = NewToken(harness.RealmA);
        await store.StoreAsync(token, default);
        var materialized = await store.GetAsync("rt-handle", default);
        // Captured before the other writer moves it: this is the version this caller believes it holds.
        var staleVersion = materialized!.StateVersion;

        // Someone else moved the token first.
        await Versioned(harness, harness.RealmA).TryUpdateAsync(materialized, staleVersion, default);

        var transition = await Versioned(harness, harness.RealmA)
            .TryConsumeAsync("rt-handle", staleVersion, Start.AddMinutes(1), default);

        Assert.Equal(RefreshTokenTransitionOutcome.Conflict, transition.Outcome);
        Assert.Null((await store.GetAsync("rt-handle", default))!.ConsumedTime);
    }

    [Fact]
    public async Task TryConsume_UnknownHandle_ReportsNotFound()
    {
        await using var harness = await SqliteOperationalStorageHarness.CreateConcreteAsync();

        var transition = await Versioned(harness, harness.RealmA)
            .TryConsumeAsync("never-issued", 0, Start, default);

        Assert.Equal(RefreshTokenTransitionOutcome.NotFound, transition.Outcome);
    }

    // DF12: the later update of a reusable token is conditional too, so a concurrent writer is not lost.
    [Fact]
    public async Task TryUpdate_WithAStaleVersion_IsAConflict_AndDoesNotWrite()
    {
        await using var harness = await SqliteOperationalStorageHarness.CreateConcreteAsync();
        var store = harness.Storage.GetRefreshTokenStore(harness.RealmA);
        await store.StoreAsync(NewToken(harness.RealmA), default);
        var first = await store.GetAsync("rt-handle", default);
        var second = await store.GetAsync("rt-handle", default);
        var versioned = Versioned(harness, harness.RealmA);

        Assert.True((await versioned.TryUpdateAsync(first!, first!.StateVersion, default)).IsSuccess);

        second!.ConsumedTime = Start.AddHours(5);
        var conflicted = await versioned.TryUpdateAsync(second, second.StateVersion, default);

        Assert.Equal(RefreshTokenTransitionOutcome.Conflict, conflicted.Outcome);
        Assert.Null((await store.GetAsync("rt-handle", default))!.ConsumedTime);
    }

    // The sequence the handler actually performs: consume, then update the reusable token. The update must use
    // the version the consumption produced — the instance loaded before it is already stale. This is the whole
    // integration between MP-3 and the handler: only the rematerialized state carries the winning version.
    [Fact]
    public async Task ConsumeThenUpdate_UsingTheRematerializedToken_Succeeds()
    {
        await using var harness = await SqliteOperationalStorageHarness.CreateConcreteAsync();
        var store = harness.Storage.GetRefreshTokenStore(harness.RealmA);
        await store.StoreAsync(NewToken(harness.RealmA), default);
        var loaded = await store.GetAsync("rt-handle", default);
        var versioned = Versioned(harness, harness.RealmA);

        var consumed = await versioned.TryConsumeAsync(
            "rt-handle", loaded!.StateVersion, Start.AddMinutes(1), default);
        Assert.True(consumed.IsSuccess);

        // The handler continues with transition.Current, not with `loaded`.
        var effective = consumed.Current!;
        var updated = await versioned.TryUpdateAsync(effective, effective.StateVersion, default);

        Assert.True(updated.IsSuccess);
    }

    // ...and the falsifiable half: carrying on with the pre-transition instance always conflicts.
    [Fact]
    public async Task ConsumeThenUpdate_UsingTheStaleInstance_Conflicts()
    {
        await using var harness = await SqliteOperationalStorageHarness.CreateConcreteAsync();
        var store = harness.Storage.GetRefreshTokenStore(harness.RealmA);
        await store.StoreAsync(NewToken(harness.RealmA), default);
        var loaded = await store.GetAsync("rt-handle", default);
        var staleVersion = loaded!.StateVersion;
        var versioned = Versioned(harness, harness.RealmA);

        await versioned.TryConsumeAsync("rt-handle", staleVersion, Start.AddMinutes(1), default);
        var updated = await versioned.TryUpdateAsync(loaded, staleVersion, default);

        Assert.Equal(RefreshTokenTransitionOutcome.Conflict, updated.Outcome);
    }

    // A successful update must actually persist what changed — otherwise "success" would mean nothing.
    [Fact]
    public async Task TryUpdate_OnSuccess_PersistsTheChanges()
    {
        await using var harness = await SqliteOperationalStorageHarness.CreateConcreteAsync();
        var store = harness.Storage.GetRefreshTokenStore(harness.RealmA);
        await store.StoreAsync(NewToken(harness.RealmA), default);
        var loaded = await store.GetAsync("rt-handle", default);

        loaded!.ConsumedTime = Start.AddMinutes(3);
        loaded.Claims.Add(new Claim("added-by-update", "yes"));
        var updated = await Versioned(harness, harness.RealmA)
            .TryUpdateAsync(loaded, loaded.StateVersion, default);

        Assert.True(updated.IsSuccess);

        var reloaded = await store.GetAsync("rt-handle", default);
        Assert.Equal(Start.AddMinutes(3), reloaded!.ConsumedTime);
        Assert.Contains(reloaded.Claims, claim => claim.Type == "added-by-update");
        // The version moved, so the instance that produced this write cannot silently write again.
        Assert.NotEqual(0, reloaded.StateVersion);
    }

    // RT-02: the read returns an expired or consumed token — the tolerance policy needs to see it.
    [Fact]
    public async Task Get_ReturnsExpiredAndConsumedTokens()
    {
        await using var harness = await SqliteOperationalStorageHarness.CreateConcreteAsync();
        var store = harness.Storage.GetRefreshTokenStore(harness.RealmA);
        await store.StoreAsync(NewToken(harness.RealmA, lifetime: 1), default);
        var materialized = await store.GetAsync("rt-handle", default);
        await Versioned(harness, harness.RealmA)
            .TryConsumeAsync("rt-handle", materialized!.StateVersion, Start.AddMinutes(1), default);

        var found = await store.GetAsync("rt-handle", default);

        Assert.NotNull(found);
        Assert.Equal(1, found.Lifetime);
        Assert.Equal(Start.AddMinutes(1), found.ConsumedTime);
    }

    // RT-05: revocation by subject reports the effective count, repeats return zero and never cross realms.
    [Fact]
    public async Task RemoveBySubject_ReportsTheEffectiveCount_AndNeverCrossesRealms()
    {
        await using var harness = await SqliteOperationalStorageHarness.CreateConcreteAsync();
        var storeA = harness.Storage.GetRefreshTokenStore(harness.RealmA);
        await storeA.StoreAsync(NewToken(harness.RealmA, "rt-1", "subject-a"), default);
        await storeA.StoreAsync(NewToken(harness.RealmA, "rt-2", "subject-a"), default);
        await storeA.StoreAsync(NewToken(harness.RealmA, "rt-3", "subject-b"), default);
        await harness.Storage.GetRefreshTokenStore(harness.RealmB)
            .StoreAsync(NewToken(harness.RealmB, "rt-4", "subject-a"), default);

        var removed = await storeA.RemoveBySubjectAsync("subject-a", default);
        var repeated = await storeA.RemoveBySubjectAsync("subject-a", default);

        Assert.Equal(2, removed);
        Assert.Equal(0, repeated);
        Assert.NotNull(await storeA.GetAsync("rt-3", default));
        Assert.NotNull(await harness.Storage.GetRefreshTokenStore(harness.RealmB).GetAsync("rt-4", default));
    }

    // DF10: the subject filter is Ordinal.
    [Fact]
    public async Task RemoveBySubject_IsOrdinal()
    {
        await using var harness = await SqliteOperationalStorageHarness.CreateConcreteAsync();
        var store = harness.Storage.GetRefreshTokenStore(harness.RealmA);
        await store.StoreAsync(NewToken(harness.RealmA, "rt-lower", "subject-case"), default);
        await store.StoreAsync(NewToken(harness.RealmA, "rt-upper", "SUBJECT-CASE"), default);

        var removed = await store.RemoveBySubjectAsync("subject-case", default);

        Assert.Equal(1, removed);
        Assert.NotNull(await store.GetAsync("rt-upper", default));
    }

    // A record whose claims mode is missing or unknown is corrupt data, not a token to guess a policy for.
    [Theory]
    [InlineData(null)]
    [InlineData(42)]
    public async Task Get_WithAMissingOrUnknownClaimsMode_FailsClosed(int? persistedMode)
    {
        await using var harness = await SqliteOperationalStorageHarness.CreateConcreteAsync();
        await harness.Storage.GetRefreshTokenStore(harness.RealmA).StoreAsync(NewToken(harness.RealmA), default);

        await using (var context = harness.NewOperationalContext())
        {
            var row = await context.Set<ProtocolArtifactEntity>()
                .SingleAsync(artifact => artifact.ArtifactType == ProtocolArtifactTypes.RefreshToken);
            row.ClaimsMode = persistedMode;
            await context.SaveChangesAsync();
        }

        await Assert.ThrowsAsync<OperationalPayloadException>(
            async () => await harness.Storage.GetRefreshTokenStore(harness.RealmA).GetAsync("rt-handle", default));
    }

    // DF9: what a read returns is an independent graph.
    [Fact]
    public async Task MutatingAMaterializedToken_DoesNotPersistImplicitly()
    {
        await using var harness = await SqliteOperationalStorageHarness.CreateConcreteAsync();
        var store = harness.Storage.GetRefreshTokenStore(harness.RealmA);
        await store.StoreAsync(NewToken(harness.RealmA), default);

        var first = await store.GetAsync("rt-handle", default);
        first!.ConsumedTime = Start.AddDays(1);
        first.Claims.Add(new Claim("injected", "true"));

        var second = await store.GetAsync("rt-handle", default);
        Assert.Null(second!.ConsumedTime);
        Assert.DoesNotContain(second.Claims, claim => claim.Type == "injected");
    }

    // DF19: a pre-cancelled token stops the operation before it reaches the provider.
    [Fact]
    public async Task PreCancelledToken_IsPropagatedToTheProvider()
    {
        await using var harness = await SqliteOperationalStorageHarness.CreateConcreteAsync();
        var store = harness.Storage.GetRefreshTokenStore(harness.RealmA);
        await store.StoreAsync(NewToken(harness.RealmA), default);
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await store.GetAsync("rt-handle", cancelled.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await store.RemoveAsync("rt-handle", cancelled.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await store.RemoveBySubjectAsync("subject-a", cancelled.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await Versioned(harness, harness.RealmA)
                .TryConsumeAsync("rt-handle", 0, Start, cancelled.Token));
    }
}
