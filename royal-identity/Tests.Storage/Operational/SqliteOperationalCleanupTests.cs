using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using RoyalIdentity.Data.Operational.Entities;
using RoyalIdentity.Models;
using RoyalIdentity.Models.Scopes;
using RoyalIdentity.Models.Tokens;
using RoyalIdentity.Storage.EntityFramework.Operational.Maintenance;
using RoyalIdentity.Users;
using Tests.Storage.Operational.Support;

namespace Tests.Storage.Operational;

/// <summary>
/// Physical cleanup by record type (plan Fase 6, MP-6/DF17). The eligibility of each lifecycle is asserted
/// individually, and so is what must <b>not</b> be removed — a consent without expiration, a refresh token still
/// observable through the configured tolerance, anything that has not expired yet. There is no retention grace:
/// a record becomes eligible when it stops being observable, not a configurable while later.
/// </summary>
public class SqliteOperationalCleanupTests
{
    private static readonly DateTime Start = Tests.Storage.Support.StorageContractHarness.Start;

    private static IOperationalMaintenance Maintenance(SqliteOperationalStorageHarness harness)
        => harness.ScopedServices.GetRequiredService<IOperationalMaintenance>();

    private static AccessToken NewAccessToken(Realm realm, string jti, int lifetime = 3600)
    {
        var token = new AccessToken(
            "client-a", "https://issuer.contract.test", AccessTokenType.Reference, Start, lifetime, jti, "Bearer")
        {
            RealmId = realm.Id,
        };
        token.Claims.Add(new Claim("sub", "subject-a"));

        return token;
    }

    private static RefreshToken NewRefreshToken(Realm realm, string handle, int lifetime = 3600)
        => new("subject-a", "session-a", ["openid"], "client-a", "https://issuer.contract.test",
            Start, lifetime, handle)
        {
            RealmId = realm.Id,
        };

    private static AuthorizationCode NewCode(Realm realm, int lifetime = 300)
        => new("client-a", new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "subject-a")], "contract")),
            "session-state", Start, lifetime, new RequestedResources(), "https://client.contract.test/callback")
        {
            RealmId = realm.Id,
        };

    private static Consent NewConsent(Realm realm, string subjectId, DateTime? expiration)
    {
        var consent = new Consent
        {
            RealmId = realm.Id,
            SubjectId = subjectId,
            ClientId = "client-a",
            CreationTime = Start,
            Expiration = expiration,
        };
        consent.AddScopes([new ConsentedScope { Scope = "openid", CreationTime = Start }]);

        return consent;
    }

    private static UserSession NewSession(string sessionId, bool isActive = true, DateTime? expiresAt = null) => new()
    {
        Id = sessionId,
        SubjectId = "subject-a",
        AuthenticationMethod = "pwd",
        IdentityProvider = "local",
        StartedAt = Start,
        LastSeenAt = Start,
        IsActive = isActive,
        ExpiresAt = expiresAt,
    };

    private static async Task<int> CountAsync<TEntity>(SqliteOperationalStorageHarness harness)
        where TEntity : class
    {
        await using var context = harness.NewOperationalContext();

        return await context.Set<TEntity>().AsNoTracking().CountAsync();
    }

    // An expired access token and an abandoned code are eligible; a live one is not.
    [Fact]
    public async Task Cleanup_RemovesExpiredArtifacts_AndKeepsLiveOnes()
    {
        await using var harness = await SqliteOperationalStorageHarness.CreateConcreteAsync();
        var realm = harness.RealmA;
        await harness.Storage.GetAccessTokenStore(realm).StoreAsync(NewAccessToken(realm, "at-expired", 60), default);
        await harness.Storage.GetAccessTokenStore(realm).StoreAsync(NewAccessToken(realm, "at-live", 86400), default);
        await harness.Storage.GetAuthorizationCodeStore(realm)
            .StoreAuthorizationCodeAsync(NewCode(realm, 60), default);
        await harness.Storage.GetAuthorizationCodeStore(realm)
            .StoreAuthorizationCodeAsync(NewCode(realm, 86400), default);

        var report = await Maintenance(harness).CleanupAsync(Start.AddHours(1), 100);

        Assert.Equal(1, report.AccessTokens);
        Assert.Equal(1, report.AuthorizationCodes);
        Assert.NotNull(await harness.Storage.GetAccessTokenStore(realm).GetAsync("at-live", default));
        Assert.Null(await harness.Storage.GetAccessTokenStore(realm).GetAsync("at-expired", default));
    }

    // DF17: with no configured maximum tolerance, a consumed refresh token is only removed when it expires —
    // the conservative reading, because the tolerance itself is per-client Configuration data.
    [Fact]
    public async Task Cleanup_WithoutAConfiguredTolerance_KeepsAConsumedButUnexpiredRefreshToken()
    {
        await using var harness = await SqliteOperationalStorageHarness.CreateConcreteAsync();
        var realm = harness.RealmA;
        var store = harness.Storage.GetRefreshTokenStore(realm);
        await store.StoreAsync(NewRefreshToken(realm, "rt-consumed", 86400), default);
        var loaded = await store.GetAsync("rt-consumed", default);
        await ((RoyalIdentity.Contracts.Storage.IVersionedRefreshTokenStore)store)
            .TryConsumeAsync("rt-consumed", loaded!.StateVersion, Start, default);

        var report = await Maintenance(harness).CleanupAsync(Start.AddHours(1), 100);

        Assert.Equal(0, report.RefreshTokens);
        Assert.NotNull(await store.GetAsync("rt-consumed", default));
    }

    // ...and with a maximum tolerance configured, it becomes eligible only after that window — never before, or
    // cleanup would delete a token a client would still accept.
    [Fact]
    public async Task Cleanup_WithAConfiguredTolerance_RemovesAConsumedTokenOnlyAfterTheWindow()
    {
        await using var harness = await SqliteOperationalStorageHarness.CreateConcreteAsync(
            cleanup: options => options.MaxRefreshTokenPostConsumedTolerance = TimeSpan.FromMinutes(30));
        var realm = harness.RealmA;
        var store = harness.Storage.GetRefreshTokenStore(realm);
        await store.StoreAsync(NewRefreshToken(realm, "rt-consumed", 86400), default);
        var loaded = await store.GetAsync("rt-consumed", default);
        await ((RoyalIdentity.Contracts.Storage.IVersionedRefreshTokenStore)store)
            .TryConsumeAsync("rt-consumed", loaded!.StateVersion, Start, default);

        // Still inside the tolerance.
        Assert.Equal(0, (await Maintenance(harness).CleanupAsync(Start.AddMinutes(29), 100)).RefreshTokens);
        Assert.NotNull(await store.GetAsync("rt-consumed", default));

        // Exactly at the boundary the handler still accepts the token, so cleanup must still keep it:
        // `IsWithinTolerance` accepts while `!(now > consumedAt + tolerance)`.
        Assert.Equal(0, (await Maintenance(harness).CleanupAsync(Start.AddMinutes(30), 100)).RefreshTokens);
        Assert.NotNull(await store.GetAsync("rt-consumed", default));

        // Past it.
        Assert.Equal(1, (await Maintenance(harness).CleanupAsync(Start.AddMinutes(31), 100)).RefreshTokens);
        Assert.Null(await store.GetAsync("rt-consumed", default));
    }

    /// <summary>
    /// The cleanup boundary must agree with the core, record type by record type. Access tokens, codes,
    /// consents and refresh tokens are validated with a strict <c>now &gt; expiration</c>, so at the expiration
    /// instant itself they are still valid and must survive the pass. Sessions and authorize parameters are
    /// inclusive on both sides — `DefaultUserSessionService` ends at `now >= expiresAt` and the AP read is
    /// fail-closed at `<=` — so at the boundary they are already gone and cleanup may take them.
    /// </summary>
    [Fact]
    public async Task Cleanup_AtTheExactExpirationInstant_AgreesWithTheCoreValidators()
    {
        await using var harness = await SqliteOperationalStorageHarness.CreateConcreteAsync();
        var realm = harness.RealmA;
        realm.Options.Authentication.AuthorizationInteractionLifetime = 60;

        await harness.Storage.GetAccessTokenStore(realm).StoreAsync(NewAccessToken(realm, "at-boundary", 60), default);
        await harness.Storage.GetAuthorizationCodeStore(realm)
            .StoreAuthorizationCodeAsync(NewCode(realm, 60), default);
        await harness.Storage.GetRefreshTokenStore(realm)
            .StoreAsync(NewRefreshToken(realm, "rt-boundary", 60), default);
        await harness.Storage.GetUserConsentStore(realm)
            .StoreUserConsentAsync(NewConsent(realm, "subject-boundary", Start.AddSeconds(60)), default);
        await harness.Storage.GetUserSessionStore(realm)
            .CreateAsync(NewSession("sid-boundary", expiresAt: Start.AddSeconds(60)));
        await harness.Storage.GetAuthorizeParametersStore(realm)
            .WriteAsync(new System.Collections.Specialized.NameValueCollection { ["client_id"] = "a" }, default);

        var atTheBoundary = await Maintenance(harness).CleanupAsync(Start.AddSeconds(60), 100);

        // Strict: still valid to the core, so still here.
        Assert.Equal(0, atTheBoundary.AccessTokens);
        Assert.Equal(0, atTheBoundary.AuthorizationCodes);
        Assert.Equal(0, atTheBoundary.RefreshTokens);
        Assert.Equal(0, atTheBoundary.Consents);
        // Inclusive: already over to the core, so already eligible.
        Assert.Equal(1, atTheBoundary.UserSessions);
        Assert.Equal(1, atTheBoundary.AuthorizeParameters);

        // One tick past the boundary the strict ones become eligible too.
        var afterwards = await Maintenance(harness).CleanupAsync(Start.AddSeconds(60).AddTicks(1), 100);

        Assert.Equal(1, afterwards.AccessTokens);
        Assert.Equal(1, afterwards.AuthorizationCodes);
        Assert.Equal(1, afterwards.RefreshTokens);
        Assert.Equal(1, afterwards.Consents);
    }

    // DF17: a consent without expiration is never eligible; only an explicit removal or a purge takes it.
    [Fact]
    public async Task Cleanup_RemovesExpiredConsents_ButNeverOnesWithoutExpiration()
    {
        await using var harness = await SqliteOperationalStorageHarness.CreateConcreteAsync();
        var realm = harness.RealmA;
        var store = harness.Storage.GetUserConsentStore(realm);
        await store.StoreUserConsentAsync(NewConsent(realm, "subject-expired", Start.AddMinutes(1)), default);
        await store.StoreUserConsentAsync(NewConsent(realm, "subject-forever", null), default);

        var report = await Maintenance(harness).CleanupAsync(Start.AddHours(1), 100);

        Assert.Equal(1, report.Consents);
        Assert.Null(await store.GetUserConsentAsync("subject-expired", "client-a", default));
        Assert.NotNull(await store.GetUserConsentAsync("subject-forever", "client-a", default));
    }

    // DF17: a session is eligible once it expired or reached its terminal state; its clients go with it.
    [Fact]
    public async Task Cleanup_RemovesEndedAndExpiredSessions_WithTheirClients()
    {
        await using var harness = await SqliteOperationalStorageHarness.CreateConcreteAsync();
        var realm = harness.RealmA;
        var store = harness.Storage.GetUserSessionStore(realm);
        await store.CreateAsync(NewSession("sid-active"));
        await store.CreateAsync(NewSession("sid-expired", expiresAt: Start.AddMinutes(1)));
        await store.CreateAsync(NewSession("sid-ended"));
        await store.RecordClientAsync("sid-ended", "client-a");
        await store.EndAsync("sid-ended");

        var report = await Maintenance(harness).CleanupAsync(Start.AddHours(1), 100);

        Assert.Equal(2, report.UserSessions);
        Assert.NotNull(await store.FindByIdAsync("sid-active"));
        Assert.Null(await store.FindByIdAsync("sid-ended"));
        Assert.Equal(0, await CountAsync<UserSessionClientEntity>(harness));
    }

    [Fact]
    public async Task Cleanup_RemovesExpiredAuthorizeParameters()
    {
        await using var harness = await SqliteOperationalStorageHarness.CreateConcreteAsync();
        var realm = harness.RealmA;
        realm.Options.Authentication.AuthorizationInteractionLifetime = 60;
        await harness.Storage.GetAuthorizeParametersStore(realm)
            .WriteAsync(new System.Collections.Specialized.NameValueCollection { ["client_id"] = "a" }, default);

        Assert.Equal(0, (await Maintenance(harness).CleanupAsync(Start.AddSeconds(30), 100)).AuthorizeParameters);
        Assert.Equal(1, (await Maintenance(harness).CleanupAsync(Start.AddSeconds(61), 100)).AuthorizeParameters);
        Assert.Equal(0, await CountAsync<AuthorizeParametersEntity>(harness));
    }

    // DF16/DF17: turning the realm's gate off later must not strand the records written while it was on —
    // maintenance is physical and never reads realm options.
    [Fact]
    public async Task Cleanup_RemovesAuthorizeParameters_EvenAfterTheRealmTurnedTheGateOff()
    {
        await using var harness = await SqliteOperationalStorageHarness.CreateConcreteAsync();
        var realm = harness.RealmA;
        realm.Options.Authentication.AuthorizationInteractionLifetime = 60;
        realm.Options.StoreAuthorizationParameters = true;
        await harness.Storage.GetAuthorizeParametersStore(realm)
            .WriteAsync(new System.Collections.Specialized.NameValueCollection { ["client_id"] = "a" }, default);

        realm.Options.StoreAuthorizationParameters = false;

        Assert.Equal(1, (await Maintenance(harness).CleanupAsync(Start.AddSeconds(61), 100)).AuthorizeParameters);
        Assert.Equal(0, await CountAsync<AuthorizeParametersEntity>(harness));
    }

    // A cleanup pass never removes more than its batch, and repeating it makes progress instead of stalling.
    [Fact]
    public async Task Cleanup_RespectsTheBatchSize_AndRepeatingMakesProgress()
    {
        await using var harness = await SqliteOperationalStorageHarness.CreateConcreteAsync();
        var realm = harness.RealmA;
        for (var index = 0; index < 5; index++)
            await harness.Storage.GetAccessTokenStore(realm).StoreAsync(NewAccessToken(realm, $"at-{index}", 60), default);

        Assert.Equal(2, (await Maintenance(harness).CleanupAsync(Start.AddHours(1), 2)).AccessTokens);
        Assert.Equal(2, (await Maintenance(harness).CleanupAsync(Start.AddHours(1), 2)).AccessTokens);
        Assert.Equal(1, (await Maintenance(harness).CleanupAsync(Start.AddHours(1), 2)).AccessTokens);
        Assert.Equal(0, (await Maintenance(harness).CleanupAsync(Start.AddHours(1), 2)).AccessTokens);
        Assert.Equal(0, await CountAsync<ProtocolArtifactEntity>(harness));
    }

    // Cleanup is idempotent: a second pass over the same instant removes nothing more.
    [Fact]
    public async Task Cleanup_IsIdempotent()
    {
        await using var harness = await SqliteOperationalStorageHarness.CreateConcreteAsync();
        await harness.Storage.GetAccessTokenStore(harness.RealmA)
            .StoreAsync(NewAccessToken(harness.RealmA, "at-expired", 60), default);

        Assert.Equal(1, (await Maintenance(harness).CleanupAsync(Start.AddHours(1), 100)).Total);
        Assert.Equal(0, (await Maintenance(harness).CleanupAsync(Start.AddHours(1), 100)).Total);
    }

    // Cleaning an access token never invalidates a refresh token: the refresh carries its own grant (DF41).
    [Fact]
    public async Task Cleanup_OfTheAccessToken_LeavesTheRefreshTokenUsable()
    {
        await using var harness = await SqliteOperationalStorageHarness.CreateConcreteAsync();
        var realm = harness.RealmA;
        await harness.Storage.GetAccessTokenStore(realm).StoreAsync(NewAccessToken(realm, "at-expired", 60), default);
        await harness.Storage.GetRefreshTokenStore(realm)
            .StoreAsync(NewRefreshToken(realm, "rt-live", 86400), default);

        await Maintenance(harness).CleanupAsync(Start.AddHours(1), 100);

        var refreshToken = await harness.Storage.GetRefreshTokenStore(realm).GetAsync("rt-live", default);
        Assert.NotNull(refreshToken);
        Assert.Equal(["openid"], refreshToken.RequestedScopes);
    }

    [Fact]
    public void CleanupOptions_Validate_RejectsIncoherentConfiguration()
    {
        Assert.Contains(
            new OperationalCleanupOptions { Mode = CleanupExecutionMode.External, BatchSize = 0 }.Validate(),
            error => error.Contains("BatchSize", StringComparison.Ordinal));

        Assert.Contains(
            new OperationalCleanupOptions { Mode = CleanupExecutionMode.Hosted, Interval = TimeSpan.Zero }.Validate(),
            error => error.Contains("Interval", StringComparison.Ordinal));

        Assert.Contains(
            new OperationalCleanupOptions
            {
                Mode = CleanupExecutionMode.External,
                MaxRefreshTokenPostConsumedTolerance = TimeSpan.FromMinutes(-1),
            }.Validate(),
            error => error.Contains("Tolerance", StringComparison.Ordinal));

        // DF17: an absent selection is a configuration error, not a default.
        Assert.Contains(
            new OperationalCleanupOptions().Validate(),
            error => error.Contains("Mode", StringComparison.Ordinal));

        Assert.Empty(new OperationalCleanupOptions { Mode = CleanupExecutionMode.External }.Validate());
    }
}
