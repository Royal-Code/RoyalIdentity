using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using RoyalIdentity.Data.Operational.Entities;
using RoyalIdentity.Models;
using RoyalIdentity.Models.Tokens;
using RoyalIdentity.Options;
using RoyalIdentity.Storage.EntityFramework.Operational.Materialization;
using Tests.Storage.Operational.Support;

namespace Tests.Storage.Operational;

/// <summary>
/// Acceptances only the EF provider can satisfy for access tokens (plan Fase 2, DF13/DF31/DF38): create-only
/// rejection, the lookup digest always derived from the <c>jti</c>, the raw bearer absent from both column and
/// payload, the three JWT persistence modes, revocation by raw token, exact batch removal, realm isolation and
/// cancellation. The shared semantics live in the provider-neutral <c>AccessTokenStoreContractTests</c>.
/// </summary>
public class SqliteOperationalAccessTokenTests
{
    private static readonly OperationalLookupDigest Digest = new();
    private static readonly DateTime Start = Tests.Storage.Support.StorageContractHarness.Start;

    private static AccessToken NewToken(
        Realm realm,
        string jti,
        string clientId = "client-a",
        string? subjectId = "subject-a",
        AccessTokenType type = AccessTokenType.Jwt,
        string? compactToken = null)
    {
        var token = new AccessToken(
            clientId, "https://issuer.contract.test", type, Start, 3600, jti, "Bearer")
        {
            RealmId = realm.Id,
        };

        if (subjectId is not null)
            token.Claims.Add(new Claim("sub", subjectId));

        if (compactToken is not null)
            token.Token = compactToken;

        return token;
    }

    /// <summary>Reads the persisted artifacts from their own context, never from the stores' change tracker.</summary>
    private static async Task<List<ProtocolArtifactEntity>> ArtifactsAsync(
        SqliteOperationalStorageHarness harness, Realm? realm = null)
    {
        await using var context = harness.NewOperationalContext();

        return await context.Set<ProtocolArtifactEntity>()
            .AsNoTracking()
            .Where(artifact => artifact.ArtifactType == ProtocolArtifactTypes.AccessToken)
            .Where(artifact => realm == null || artifact.RealmId == realm.Id)
            .ToListAsync();
    }

    // AT-01: create-only. The primary key is the authority, so a duplicate jti in the same realm fails visibly
    // instead of silently replacing a live token.
    [Fact]
    public async Task Store_DuplicateJtiInTheSameRealm_FailsWithoutOverwriting()
    {
        await using var harness = await SqliteOperationalStorageHarness.CreateConcreteAsync();
        var store = harness.Storage.GetAccessTokenStore(harness.RealmA);
        await store.StoreAsync(NewToken(harness.RealmA, "dup-jti", clientId: "client-first"), default);

        await Assert.ThrowsAnyAsync<DbUpdateException>(
            async () => await store.StoreAsync(NewToken(harness.RealmA, "dup-jti", clientId: "client-second"), default));

        var found = await store.GetAsync("dup-jti", default);
        Assert.NotNull(found);
        Assert.Equal("client-first", found.ClientId);
    }

    // DF5: the same jti in another realm is another row, accepted and isolated.
    [Fact]
    public async Task Store_SameJtiInAnotherRealm_IsAccepted()
    {
        await using var harness = await SqliteOperationalStorageHarness.CreateConcreteAsync();
        await harness.Storage.GetAccessTokenStore(harness.RealmA)
            .StoreAsync(NewToken(harness.RealmA, "shared-jti", clientId: "client-a"), default);

        await harness.Storage.GetAccessTokenStore(harness.RealmB)
            .StoreAsync(NewToken(harness.RealmB, "shared-jti", clientId: "client-b"), default);

        Assert.Equal("client-a", (await harness.Storage.GetAccessTokenStore(harness.RealmA)
            .GetAsync("shared-jti", default))!.ClientId);
        Assert.Equal("client-b", (await harness.Storage.GetAccessTokenStore(harness.RealmB)
            .GetAsync("shared-jti", default))!.ClientId);
    }

    // DF13/DF38: the row is located by SHA-256(jti) in every mode; the raw jti has no column of its own.
    [Theory]
    [InlineData(AccessTokenType.Reference, JwtAccessTokenPersistenceMode.Full)]
    [InlineData(AccessTokenType.Jwt, JwtAccessTokenPersistenceMode.Metadata)]
    [InlineData(AccessTokenType.Jwt, JwtAccessTokenPersistenceMode.Full)]
    public async Task Store_KeysTheRowByTheDigestOfTheJti(AccessTokenType type, JwtAccessTokenPersistenceMode mode)
    {
        await using var harness = await SqliteOperationalStorageHarness.CreateConcreteAsync();
        var realm = harness.RealmA;
        realm.Options.OperationalStorage.JwtAccessTokenPersistence = mode;

        await harness.Storage.GetAccessTokenStore(realm)
            .StoreAsync(NewToken(realm, "digest-jti", type: type, compactToken: "header.payload.signature"), default);

        var row = Assert.Single(await ArtifactsAsync(harness, realm));

        Assert.Equal(Digest.Compute(OperationalRecordTypes.AccessToken, "digest-jti"), row.LookupDigest);
        Assert.DoesNotContain("digest-jti", row.LookupDigest, StringComparison.Ordinal);
    }

    // DF13: no column holds the raw jti/bearer, and a reference bearer never reaches the payload either — the
    // lookup argument is what rematerializes Id and Token.
    [Fact]
    public async Task ReferenceToken_KeepsItsBearerOutOfEveryColumnAndOutOfThePayload()
    {
        await using var harness = await SqliteOperationalStorageHarness.CreateConcreteAsync();
        const string bearer = "reference-bearer-value";
        await harness.Storage.GetAccessTokenStore(harness.RealmA)
            .StoreAsync(NewToken(harness.RealmA, bearer, type: AccessTokenType.Reference), default);

        var row = Assert.Single(await ArtifactsAsync(harness));

        Assert.DoesNotContain(bearer, row.LookupDigest, StringComparison.Ordinal);
        Assert.DoesNotContain(bearer, row.ProtectedPayload!, StringComparison.Ordinal);
        Assert.Null(row.RedirectUri);
        Assert.Null(row.ConsumedAtUtc);

        var found = await harness.Storage.GetAccessTokenStore(harness.RealmA).GetAsync(bearer, default);
        Assert.NotNull(found);
        Assert.Equal(bearer, found.Id);
        Assert.Equal(bearer, found.Token);
    }

    // DF31: None writes nothing at all — a JWT is verifiable without the store.
    [Fact]
    public async Task JwtNone_WritesNoArtifact()
    {
        await using var harness = await SqliteOperationalStorageHarness.CreateConcreteAsync();
        var realm = harness.RealmA;
        realm.Options.OperationalStorage.JwtAccessTokenPersistence = JwtAccessTokenPersistenceMode.None;
        var store = harness.Storage.GetAccessTokenStore(realm);

        var returned = await store.StoreAsync(NewToken(realm, "none-jti", compactToken: "a.b.c"), default);

        Assert.Equal("none-jti", returned);
        Assert.Empty(await ArtifactsAsync(harness, realm));
        Assert.Null(await store.GetAsync("none-jti", default));
    }

    // DF31: Metadata keeps the queryable graph but never the compact JWT.
    [Fact]
    public async Task JwtMetadata_PersistsTheGraphWithoutTheCompactToken()
    {
        await using var harness = await SqliteOperationalStorageHarness.CreateConcreteAsync();
        var realm = harness.RealmA;
        realm.Options.OperationalStorage.JwtAccessTokenPersistence = JwtAccessTokenPersistenceMode.Metadata;
        var store = harness.Storage.GetAccessTokenStore(realm);

        await store.StoreAsync(NewToken(realm, "meta-jti", compactToken: "header.payload.signature"), default);

        var row = Assert.Single(await ArtifactsAsync(harness, realm));
        Assert.DoesNotContain("header.payload.signature", row.ProtectedPayload!, StringComparison.Ordinal);

        var found = await store.GetAsync("meta-jti", default);
        Assert.NotNull(found);
        Assert.Equal("meta-jti", found.Id);
        Assert.Equal("subject-a", found.SubjectId);
        // With no compact JWT persisted, the token string falls back to the lookup argument.
        Assert.Equal("meta-jti", found.Token);
    }

    // DF31: Full round-trips the compact JWT, which is not the lookup key.
    [Fact]
    public async Task JwtFull_RoundTripsTheCompactToken()
    {
        await using var harness = await SqliteOperationalStorageHarness.CreateConcreteAsync();
        var realm = harness.RealmA;
        realm.Options.OperationalStorage.JwtAccessTokenPersistence = JwtAccessTokenPersistenceMode.Full;
        var store = harness.Storage.GetAccessTokenStore(realm);

        await store.StoreAsync(NewToken(realm, "full-jti", compactToken: "header.payload.signature"), default);
        var found = await store.GetAsync("full-jti", default);

        Assert.NotNull(found);
        Assert.Equal("full-jti", found.Id);
        Assert.Equal("header.payload.signature", found.Token);
        Assert.Null(await store.GetAsync("header.payload.signature", default));
    }

    // DF31: the policy is captured at write time; changing the realm option later never reinterprets rows.
    [Fact]
    public async Task ChangingTheJwtMode_DoesNotReinterpretExistingArtifacts()
    {
        await using var harness = await SqliteOperationalStorageHarness.CreateConcreteAsync();
        var realm = harness.RealmA;
        realm.Options.OperationalStorage.JwtAccessTokenPersistence = JwtAccessTokenPersistenceMode.Full;
        await harness.Storage.GetAccessTokenStore(realm)
            .StoreAsync(NewToken(realm, "kept-jti", compactToken: "header.payload.signature"), default);

        realm.Options.OperationalStorage.JwtAccessTokenPersistence = JwtAccessTokenPersistenceMode.None;
        var found = await harness.Storage.GetAccessTokenStore(realm).GetAsync("kept-jti", default);

        Assert.NotNull(found);
        Assert.Equal("header.payload.signature", found.Token);
    }

    // DF30: two realms with different profiles do not share keys; each row records the profile that wrote it.
    [Fact]
    public async Task TwoRealms_WithDifferentProtectionProfiles_KeepTheirOwnEnvelopes()
    {
        await using var harness = await SqliteOperationalStorageHarness.CreateConcreteAsync();
        harness.RealmB.Options.OperationalStorage.PayloadProtectionProfile =
            SqliteOperationalStorageHarness.AlternateProtectionProfile;

        await harness.Storage.GetAccessTokenStore(harness.RealmA)
            .StoreAsync(NewToken(harness.RealmA, "profile-a"), default);
        await harness.Storage.GetAccessTokenStore(harness.RealmB)
            .StoreAsync(NewToken(harness.RealmB, "profile-b"), default);

        Assert.NotNull(await harness.Storage.GetAccessTokenStore(harness.RealmA).GetAsync("profile-a", default));
        Assert.NotNull(await harness.Storage.GetAccessTokenStore(harness.RealmB).GetAsync("profile-b", default));

        var rows = await ArtifactsAsync(harness);
        Assert.Equal(2, rows.Count);
        Assert.Contains(rows, row => row.ProtectedPayload!.StartsWith(
            $"v1:{SqliteOperationalStorageHarness.DefaultProtectionProfile}:", StringComparison.Ordinal));
        Assert.Contains(rows, row => row.ProtectedPayload!.StartsWith(
            $"v1:{SqliteOperationalStorageHarness.AlternateProtectionProfile}:", StringComparison.Ordinal));
    }

    // Two realms can run different JWT modes at the same time without one policy leaking into the other.
    [Fact]
    public async Task TwoRealms_WithDifferentJwtModes_DoNotShareThePolicy()
    {
        await using var harness = await SqliteOperationalStorageHarness.CreateConcreteAsync();
        harness.RealmA.Options.OperationalStorage.JwtAccessTokenPersistence = JwtAccessTokenPersistenceMode.None;
        harness.RealmB.Options.OperationalStorage.JwtAccessTokenPersistence = JwtAccessTokenPersistenceMode.Full;

        await harness.Storage.GetAccessTokenStore(harness.RealmA)
            .StoreAsync(NewToken(harness.RealmA, "mode-a"), default);
        await harness.Storage.GetAccessTokenStore(harness.RealmB)
            .StoreAsync(NewToken(harness.RealmB, "mode-b"), default);

        Assert.Empty(await ArtifactsAsync(harness, harness.RealmA));
        Assert.Single(await ArtifactsAsync(harness, harness.RealmB));
    }

    // DF13: revocation by raw token finds a reference token, because its bearer is the jti. A JWT persisted as
    // metadata/full is not found by its compact form, so persisting it never simulates stateful revocation.
    [Fact]
    public async Task RevocationByRawToken_FindsReferenceTokens_ButNeverACompactJwt()
    {
        await using var harness = await SqliteOperationalStorageHarness.CreateConcreteAsync();
        var realm = harness.RealmA;
        var store = harness.Storage.GetAccessTokenStore(realm);
        await store.StoreAsync(NewToken(realm, "ref-bearer", type: AccessTokenType.Reference), default);
        await store.StoreAsync(NewToken(realm, "jwt-jti", compactToken: "header.payload.signature"), default);

        Assert.NotNull(await store.GetAsync("ref-bearer", default));
        Assert.Null(await store.GetAsync("header.payload.signature", default));

        await store.RemoveAsync("ref-bearer", default);
        await store.RemoveAsync("header.payload.signature", default);

        Assert.Null(await store.GetAsync("ref-bearer", default));
        // The JWT artifact survives: nothing addressed it, which is exactly the documented behavior.
        Assert.NotNull(await store.GetAsync("jwt-jti", default));
    }

    // AT-04: the batch removal filter is exact — type, subject, client and realm.
    [Fact]
    public async Task RemoveReferenceTokens_RemovesOnlyTheMatchingReferenceTokens()
    {
        await using var harness = await SqliteOperationalStorageHarness.CreateConcreteAsync();
        var store = harness.Storage.GetAccessTokenStore(harness.RealmA);
        await store.StoreAsync(
            NewToken(harness.RealmA, "ref-match", "client-a", "subject-a", AccessTokenType.Reference), default);
        await store.StoreAsync(
            NewToken(harness.RealmA, "ref-other-client", "client-b", "subject-a", AccessTokenType.Reference), default);
        await store.StoreAsync(
            NewToken(harness.RealmA, "ref-other-subject", "client-a", "subject-b", AccessTokenType.Reference), default);
        await store.StoreAsync(NewToken(harness.RealmA, "jwt-same-pair", "client-a", "subject-a"), default);
        await harness.Storage.GetAccessTokenStore(harness.RealmB).StoreAsync(
            NewToken(harness.RealmB, "ref-other-realm", "client-a", "subject-a", AccessTokenType.Reference), default);

        await store.RemoveReferenceTokensAsync("subject-a", "client-a", default);

        Assert.Null(await store.GetAsync("ref-match", default));
        Assert.NotNull(await store.GetAsync("ref-other-client", default));
        Assert.NotNull(await store.GetAsync("ref-other-subject", default));
        Assert.NotNull(await store.GetAsync("jwt-same-pair", default));
        Assert.NotNull(await harness.Storage.GetAccessTokenStore(harness.RealmB).GetAsync("ref-other-realm", default));
    }

    // AT-04 is idempotent: repeating it removes nothing more and does not fail.
    [Fact]
    public async Task RemoveReferenceTokens_IsIdempotent()
    {
        await using var harness = await SqliteOperationalStorageHarness.CreateConcreteAsync();
        var store = harness.Storage.GetAccessTokenStore(harness.RealmA);
        await store.StoreAsync(NewToken(harness.RealmA, "ref-idem", type: AccessTokenType.Reference), default);

        await store.RemoveReferenceTokensAsync("subject-a", "client-a", default);
        await store.RemoveReferenceTokensAsync("subject-a", "client-a", default);

        Assert.Empty(await ArtifactsAsync(harness, harness.RealmA));
    }

    // The persisted type decides whether a token may be presented as an opaque bearer, so it is never guessed:
    // a row whose access_token_type is missing or unknown is corrupt data and fails closed instead of
    // materializing as a plausible default.
    [Theory]
    [InlineData(null)]
    [InlineData(42)]
    public async Task Get_WithAMissingOrUnknownAccessTokenType_FailsClosed(int? persistedType)
    {
        await using var harness = await SqliteOperationalStorageHarness.CreateConcreteAsync();
        var store = harness.Storage.GetAccessTokenStore(harness.RealmA);
        await store.StoreAsync(NewToken(harness.RealmA, "corrupt-type"), default);

        await using (var context = harness.NewOperationalContext())
        {
            var row = await context.Set<ProtocolArtifactEntity>().SingleAsync();
            row.AccessTokenType = persistedType;
            await context.SaveChangesAsync();
        }

        await Assert.ThrowsAsync<OperationalPayloadException>(
            async () => await harness.Storage.GetAccessTokenStore(harness.RealmA).GetAsync("corrupt-type", default));
    }

    // DF9: what a read returns is an independent graph; mutating it never reaches the database.
    [Fact]
    public async Task MutatingAMaterializedToken_DoesNotPersistImplicitly()
    {
        await using var harness = await SqliteOperationalStorageHarness.CreateConcreteAsync();
        var store = harness.Storage.GetAccessTokenStore(harness.RealmA);
        await store.StoreAsync(NewToken(harness.RealmA, "mutate-jti"), default);

        var first = await store.GetAsync("mutate-jti", default);
        first!.Audiences.Add("https://injected.example");
        first.Claims.Add(new Claim("injected", "true"));

        var second = await store.GetAsync("mutate-jti", default);
        Assert.DoesNotContain("https://injected.example", second!.Audiences);
        Assert.DoesNotContain(second.Claims, claim => claim.Type == "injected");
    }

    // DF19: a pre-cancelled token stops the operation before it reaches the provider.
    [Fact]
    public async Task PreCancelledToken_IsPropagatedToTheProvider()
    {
        await using var harness = await SqliteOperationalStorageHarness.CreateConcreteAsync();
        var store = harness.Storage.GetAccessTokenStore(harness.RealmA);
        await store.StoreAsync(NewToken(harness.RealmA, "cancel-jti"), default);
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await store.GetAsync("cancel-jti", cancelled.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await store.RemoveAsync("cancel-jti", cancelled.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await store.StoreAsync(NewToken(harness.RealmA, "cancel-jti-2"), cancelled.Token));
    }
}
