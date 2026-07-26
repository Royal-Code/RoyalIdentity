using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using RoyalIdentity.Data.Operational.Entities;
using RoyalIdentity.Models;
using RoyalIdentity.Models.Tokens;
using RoyalIdentity.Options;
using RoyalIdentity.Storage.EntityFramework.Operational.Maintenance;
using RoyalIdentity.Storage.EntityFramework.Operational.Stores;
using Tests.Storage.Operational.Support;

namespace Tests.Storage.Operational;

/// <summary>
/// The per-realm policies of the Operational family, asserted identically on every provider (plan Fase 7).
/// The provider-neutral contracts and the EF parity suites cover casing, duplicates, absence, TTL and counts on
/// both providers. What remains here — and what a schema/type/collation difference could silently break — are
/// the three JWT persistence modes (DF31), the two refresh claims modes (DF32/DF33) and the per-realm protection
/// profile (DF30). These scenarios exist to answer one question: do the providers agree?
/// </summary>
public abstract class OperationalProviderParityTests : OperationalParitySuite
{
    private static readonly DateTime Start = Tests.Storage.Support.StorageContractHarness.Start;

    private static AccessToken NewAccessToken(Realm realm, string jti, string? compactToken = null)
    {
        var token = new AccessToken(
            "client-a", "https://issuer.contract.test", AccessTokenType.Jwt, Start, 3600,
            jti, "Bearer")
        {
            RealmId = realm.Id,
        };
        token.Claims.Add(new Claim("sub", "subject-a"));

        if (compactToken is not null)
            token.Token = compactToken;

        return token;
    }

    private static RefreshToken NewRefreshToken(Realm realm, string handle, RefreshTokenClaimsMode claimsMode)
    {
        var token = new RefreshToken(
            "subject-a", "session-a", ["openid"], "client-a", "https://issuer.contract.test",
            Start, 3600, handle)
        {
            RealmId = realm.Id,
            ClaimsMode = claimsMode,
        };

        if (claimsMode is RefreshTokenClaimsMode.Snapshot)
        {
            token.Claims.Add(new Claim("client_id", "client-a"));
            token.IdentityTokenClaims.Add(new Claim("website", "https://snapshot.example"));
        }

        return token;
    }

    private static async Task<List<ProtocolArtifactEntity>> ArtifactsAsync(
        IOperationalParityHarness harness, Realm? realm = null)
    {
        await using var context = harness.NewOperationalContext();
        var query = context.Set<ProtocolArtifactEntity>().AsNoTracking();

        if (realm is not null)
            query = query.Where(artifact => artifact.RealmId == realm.Id);

        return await query.ToListAsync();
    }

    // DF31: None writes nothing at all — a JWT is verifiable without the store.
    [Fact]
    public async Task JwtNone_WritesNoArtifact()
    {
        await using var harness = await CreateHarnessAsync();
        var realm = harness.RealmA;
        realm.Options.OperationalStorage.JwtAccessTokenPersistence = JwtAccessTokenPersistenceMode.None;
        var store = harness.Storage.GetAccessTokenStore(realm);

        var returned = await store.StoreAsync(NewAccessToken(realm, "none-jti", "a.b.c"), default);

        Assert.Equal("none-jti", returned);
        Assert.Empty(await ArtifactsAsync(harness, realm));
        Assert.Null(await store.GetAsync("none-jti", default));
    }

    // DF31: Metadata keeps the queryable graph but never the compact JWT.
    [Fact]
    public async Task JwtMetadata_PersistsTheGraphWithoutTheCompactToken()
    {
        await using var harness = await CreateHarnessAsync();
        var realm = harness.RealmA;
        realm.Options.OperationalStorage.JwtAccessTokenPersistence = JwtAccessTokenPersistenceMode.Metadata;
        var store = harness.Storage.GetAccessTokenStore(realm);

        await store.StoreAsync(NewAccessToken(realm, "meta-jti", "header.payload.signature"), default);

        var row = Assert.Single(await ArtifactsAsync(harness, realm));
        Assert.DoesNotContain("header.payload.signature", row.ProtectedPayload!, StringComparison.Ordinal);

        var found = await store.GetAsync("meta-jti", default);
        Assert.NotNull(found);
        Assert.Equal("subject-a", found.SubjectId);
        // With no compact JWT persisted, the token string falls back to the lookup argument.
        Assert.Equal("meta-jti", found.Token);
    }

    // DF31: Full round-trips the compact JWT, which is not the lookup key.
    [Fact]
    public async Task JwtFull_RoundTripsTheCompactToken()
    {
        await using var harness = await CreateHarnessAsync();
        var realm = harness.RealmA;
        realm.Options.OperationalStorage.JwtAccessTokenPersistence = JwtAccessTokenPersistenceMode.Full;
        var store = harness.Storage.GetAccessTokenStore(realm);

        await store.StoreAsync(NewAccessToken(realm, "full-jti", "header.payload.signature"), default);
        var found = await store.GetAsync("full-jti", default);

        Assert.NotNull(found);
        Assert.Equal("header.payload.signature", found.Token);
        Assert.Null(await store.GetAsync("header.payload.signature", default));
    }

    // Two realms can run different JWT modes at once without one policy leaking into the other.
    [Fact]
    public async Task TwoRealms_WithDifferentJwtModes_DoNotShareThePolicy()
    {
        await using var harness = await CreateHarnessAsync();
        harness.RealmA.Options.OperationalStorage.JwtAccessTokenPersistence = JwtAccessTokenPersistenceMode.None;
        harness.RealmB.Options.OperationalStorage.JwtAccessTokenPersistence = JwtAccessTokenPersistenceMode.Full;

        await harness.Storage.GetAccessTokenStore(harness.RealmA)
            .StoreAsync(NewAccessToken(harness.RealmA, "mode-a"), default);
        await harness.Storage.GetAccessTokenStore(harness.RealmB)
            .StoreAsync(NewAccessToken(harness.RealmB, "mode-b"), default);

        Assert.Empty(await ArtifactsAsync(harness, harness.RealmA));
        Assert.Single(await ArtifactsAsync(harness, harness.RealmB));
    }

    // DF32/DF33: the claims mode is captured at write time and survives a later realm change.
    [Theory]
    [InlineData(RefreshTokenClaimsMode.Current)]
    [InlineData(RefreshTokenClaimsMode.Snapshot)]
    public async Task RefreshClaimsMode_IsCapturedAtWriteTime(RefreshTokenClaimsMode mode)
    {
        await using var harness = await CreateHarnessAsync();
        var realm = harness.RealmA;
        await harness.Storage.GetRefreshTokenStore(realm)
            .StoreAsync(NewRefreshToken(realm, "rt-parity", mode), default);

        realm.Options.RefreshTokens.ClaimsMode = mode is RefreshTokenClaimsMode.Current
            ? RefreshTokenClaimsMode.Snapshot
            : RefreshTokenClaimsMode.Current;

        var restored = await harness.Storage.GetRefreshTokenStore(realm).GetAsync("rt-parity", default);

        Assert.NotNull(restored);
        Assert.Equal(mode, restored.ClaimsMode);

        if (mode is RefreshTokenClaimsMode.Snapshot)
        {
            Assert.Equal(
                "https://snapshot.example",
                Assert.Single(restored.IdentityTokenClaims, claim => claim.Type == "website").Value);
            Assert.Contains(restored.Claims, claim => claim.Type == "client_id");
        }
        else
        {
            Assert.Empty(restored.IdentityTokenClaims);
        }
    }

    // DF30: two realms with different profiles do not share keys; each row records the profile that wrote it.
    [Fact]
    public async Task TwoRealms_WithDifferentProtectionProfiles_KeepTheirOwnEnvelopes()
    {
        await using var harness = await CreateHarnessAsync();
        harness.RealmB.Options.OperationalStorage.PayloadProtectionProfile = harness.AlternateProfile;

        await harness.Storage.GetAccessTokenStore(harness.RealmA)
            .StoreAsync(NewAccessToken(harness.RealmA, "profile-a"), default);
        await harness.Storage.GetAccessTokenStore(harness.RealmB)
            .StoreAsync(NewAccessToken(harness.RealmB, "profile-b"), default);

        Assert.NotNull(await harness.Storage.GetAccessTokenStore(harness.RealmA).GetAsync("profile-a", default));
        Assert.NotNull(await harness.Storage.GetAccessTokenStore(harness.RealmB).GetAsync("profile-b", default));

        var rows = await ArtifactsAsync(harness);
        Assert.Equal(2, rows.Count);
        Assert.Contains(rows, row => row.ProtectedPayload!.StartsWith(
            $"v1:{harness.DefaultProfile}:", StringComparison.Ordinal));
        Assert.Contains(rows, row => row.ProtectedPayload!.StartsWith(
            $"v1:{harness.AlternateProfile}:", StringComparison.Ordinal));
    }

    // DF10: identifiers compare byte-wise on every provider, whatever the database locale happens to be.
    [Fact]
    public async Task Lookups_AreOrdinal_OnEveryProvider()
    {
        await using var harness = await CreateHarnessAsync();
        var realm = harness.RealmA;
        var store = harness.Storage.GetAccessTokenStore(realm);
        await store.StoreAsync(NewAccessToken(realm, "Casing-Jti"), default);

        Assert.NotNull(await store.GetAsync("Casing-Jti", default));
        Assert.Null(await store.GetAsync("casing-jti", default));
        Assert.Null(await store.GetAsync("CASING-JTI", default));

        // And a differently-cased id is a different row, not a duplicate.
        await store.StoreAsync(NewAccessToken(realm, "casing-jti"), default);
        Assert.Equal(2, (await ArtifactsAsync(harness, realm)).Count);
    }

    // DF19: a pre-cancelled token stops the operation before it reaches the provider — on every provider, and
    // across every store, because cancellation is honored by the adapter and not by a provider's driver.
    [Fact]
    public async Task PreCancelledToken_IsPropagated_ByEveryStore()
    {
        await using var harness = await CreateHarnessAsync();
        var realm = harness.RealmA;
        await harness.Storage.GetAccessTokenStore(realm)
            .StoreAsync(NewAccessToken(realm, "cancel-jti"), default);
        await harness.Storage.GetRefreshTokenStore(realm)
            .StoreAsync(NewRefreshToken(realm, "cancel-rt", RefreshTokenClaimsMode.Current), default);

        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await harness.Storage.GetAccessTokenStore(realm).GetAsync("cancel-jti", cancelled.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await harness.Storage.GetAccessTokenStore(realm).RemoveAsync("cancel-jti", cancelled.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await harness.Storage.GetRefreshTokenStore(realm).GetAsync("cancel-rt", cancelled.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await harness.Storage.GetUserSessionStore(realm)
                .FindByIdAsync("cancel-session", cancelled.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await harness.Storage.GetUserConsentStore(realm)
                .GetUserConsentAsync("subject-a", "client-a", cancelled.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await harness.Storage.GetAuthorizationCodeStore(realm)
                .GetAuthorizationCodeAsync("cancel-code", cancelled.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await harness.Storage.GetAuthorizeParametersStore(realm)
                .ReadAsync("cancel-handle", cancelled.Token));
    }

    /// <summary>SQLite runs this suite unconditionally; it is the baseline the other provider must match.</summary>
    public sealed class Sqlite : OperationalProviderParityTests
    {
        private protected override Task<IOperationalParityHarness> CreateHarnessAsync(
            IAuthorizeParametersHandleGenerator? handleGenerator = null,
            Action<OperationalCleanupOptions>? cleanup = null)
            => SqliteParityHarness.CreateAsync(handleGenerator, cleanup);
    }
}

/// <summary>
/// The same parity suite over PostgreSQL. The concrete suite stays private so xUnit does not discover its
/// facts when the opt-in connection is unavailable.
/// </summary>
public class PostgreSqlOperationalParityTests
{
    [Configuration.StoragePostgreSqlFact]
    [Trait("Category", "PostgreSql")]
    public Task Parity() => Configuration.Support.ProviderFactRunner.RunAsync(new PostgreSqlParity());

    private sealed class PostgreSqlParity : OperationalProviderParityTests
    {
        private protected override Task<IOperationalParityHarness> CreateHarnessAsync(
            IAuthorizeParametersHandleGenerator? handleGenerator = null,
            Action<OperationalCleanupOptions>? cleanup = null)
            => PostgreSqlParityHarness.CreateAsync(handleGenerator, cleanup);
    }
}
