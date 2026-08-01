using System.Collections.Specialized;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RoyalIdentity.Data.Configuration.Entities;
using RoyalIdentity.Data.Operational.Entities;
using RoyalIdentity.Models;
using RoyalIdentity.Models.Scopes;
using RoyalIdentity.Models.Tokens;
using RoyalIdentity.Storage.EntityFramework.Operational.Maintenance;
using RoyalIdentity.Users;
using Tests.Storage.Operational.Support;
using RoyalIdentity.Storage.EntityFramework.Operational.Stores;

namespace Tests.Storage.Operational;

/// <summary>
/// The Operational half of the realm deletion (plan Fase 6, MP-7/DF18): the purge removes every Operational
/// record of one realm, is repeatable, and never reaches Configuration — the realm tombstone and its
/// path/domain reservation live there and must survive.
/// </summary>
public abstract class OperationalPurgeRealmTests : OperationalParitySuite
{
    private static readonly DateTime Start = Tests.Storage.Support.StorageContractHarness.Start;

    private static IOperationalMaintenance Maintenance(IOperationalParityHarness harness)
        => harness.ScopedServices.GetRequiredService<IOperationalMaintenance>();

    /// <summary>Writes one row into every Operational table of a realm.</summary>
    private static async Task SeedEveryTableAsync(IOperationalParityHarness harness, Realm realm, string suffix)
    {
        var accessToken = new AccessToken(
            "client-a", "https://issuer.contract.test", AccessTokenType.Reference, Start, 3600,
            $"at-{suffix}", "Bearer")
        {
            RealmId = realm.Id,
        };
        accessToken.Claims.Add(new Claim("sub", "subject-a"));
        await harness.Storage.GetAccessTokenStore(realm).StoreAsync(accessToken, default);

        await harness.Storage.GetRefreshTokenStore(realm).StoreAsync(
            new RefreshToken("subject-a", "session-a", ["openid"], "client-a", "https://issuer.contract.test",
                Start, 3600, $"rt-{suffix}")
            {
                RealmId = realm.Id,
            },
            default);

        await harness.Storage.GetAuthorizationCodeStore(realm).StoreAuthorizationCodeAsync(
            new AuthorizationCode(
                "client-a",
                new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "subject-a")], "contract")),
                Start, 300, new RequestedResources(), "https://client.contract.test/callback")
            {
                RealmId = realm.Id,
            },
            default);

        var consent = new Consent
        {
            RealmId = realm.Id,
            SubjectId = "subject-a",
            ClientId = "client-a",
            CreationTime = Start,
        };
        consent.AddScopes([new ConsentedScope { Scope = "openid", CreationTime = Start }]);
        await harness.Storage.GetUserConsentStore(realm).StoreUserConsentAsync(consent, default);

        var sessions = harness.Storage.GetUserSessionStore(realm);
        await sessions.CreateAsync(new UserSession
        {
            Id = $"sid-{suffix}",
            SubjectId = "subject-a",
            AuthenticationMethod = "pwd",
            IdentityProvider = "local",
            StartedAt = Start,
            LastSeenAt = Start,
        });
        await sessions.RecordClientAsync($"sid-{suffix}", "client-a");

        await harness.Storage.GetAuthorizeParametersStore(realm)
            .WriteAsync(new NameValueCollection { ["client_id"] = "client-a" }, default);
    }

    private static async Task<int> CountAsync<TEntity>(IOperationalParityHarness harness, string realmId)
        where TEntity : class
    {
        await using var context = harness.NewOperationalContext();

        return await context.Set<TEntity>()
            .AsNoTracking()
            .Where(row => EF.Property<string>(row, "RealmId") == realmId)
            .CountAsync();
    }

    // The purge covers every Operational table of the target realm and leaves the other realm untouched.
    [Fact]
    public async Task Purge_RemovesEveryOperationalRecordOfTheRealm_AndOnlyThatRealm()
    {
        await using var harness = await CreateHarnessAsync();
        await SeedEveryTableAsync(harness, harness.RealmA, "a");
        await SeedEveryTableAsync(harness, harness.RealmB, "b");

        var report = await Maintenance(harness).PurgeRealmAsync(harness.RealmA.Id);

        Assert.Equal(3, report.ProtocolArtifacts);
        Assert.Equal(1, report.Consents);
        Assert.Equal(1, report.UserSessions);
        Assert.Equal(1, report.AuthorizeParameters);

        Assert.Equal(0, await CountAsync<ProtocolArtifactEntity>(harness, harness.RealmA.Id));
        Assert.Equal(0, await CountAsync<ConsentEntity>(harness, harness.RealmA.Id));
        Assert.Equal(0, await CountAsync<UserSessionEntity>(harness, harness.RealmA.Id));
        Assert.Equal(0, await CountAsync<UserSessionClientEntity>(harness, harness.RealmA.Id));
        Assert.Equal(0, await CountAsync<AuthorizeParametersEntity>(harness, harness.RealmA.Id));

        Assert.Equal(3, await CountAsync<ProtocolArtifactEntity>(harness, harness.RealmB.Id));
        Assert.Equal(1, await CountAsync<ConsentEntity>(harness, harness.RealmB.Id));
        Assert.Equal(1, await CountAsync<UserSessionEntity>(harness, harness.RealmB.Id));
        Assert.Equal(1, await CountAsync<UserSessionClientEntity>(harness, harness.RealmB.Id));
        Assert.Equal(1, await CountAsync<AuthorizeParametersEntity>(harness, harness.RealmB.Id));
    }

    // DF18: Configuration keeps the realm — the tombstone and its path/domain reservation are exactly what must
    // survive a purge, and nothing here can reach that family anyway.
    [Fact]
    public async Task Purge_DoesNotTouchConfiguration()
    {
        await using var harness = await CreateHarnessAsync();
        await SeedEveryTableAsync(harness, harness.RealmA, "a");

        await Maintenance(harness).PurgeRealmAsync(harness.RealmA.Id);

        // Both families live in the same database in these fixtures, which makes the assertion real rather than
        // a tautology of separate databases.
        var realms = await harness.CountConfigurationAsync<RealmEntity>();
        var clients = await harness.CountConfigurationAsync<ClientEntity>();

        Assert.True(realms > 0, "the realm rows must survive the Operational purge");
        Assert.NotNull(await harness.Storage.Realms.GetByIdAsync(harness.RealmA.Id, default));
        Assert.Equal(clients, await harness.CountConfigurationAsync<ClientEntity>());
    }

    [Fact]
    public async Task Purge_IsIdempotent()
    {
        await using var harness = await CreateHarnessAsync();
        await SeedEveryTableAsync(harness, harness.RealmA, "a");

        Assert.NotEqual(0, (await Maintenance(harness).PurgeRealmAsync(harness.RealmA.Id)).Total);
        Assert.Equal(0, (await Maintenance(harness).PurgeRealmAsync(harness.RealmA.Id)).Total);
        Assert.Equal(0, (await Maintenance(harness).PurgeRealmAsync("realm-that-never-existed")).Total);
    }

    // DF5: colliding ids across realms are different rows, so purging one realm cannot take the other's.
    [Fact]
    public async Task Purge_WithCollidingIdsAcrossRealms_RemovesOnlyTheTargetRealm()
    {
        await using var harness = await CreateHarnessAsync();
        // The very same handles in both realms.
        await SeedEveryTableAsync(harness, harness.RealmA, "shared");
        await SeedEveryTableAsync(harness, harness.RealmB, "shared");

        await Maintenance(harness).PurgeRealmAsync(harness.RealmA.Id);

        Assert.Null(await harness.Storage.GetAccessTokenStore(harness.RealmA).GetAsync("at-shared", default));
        Assert.NotNull(await harness.Storage.GetAccessTokenStore(harness.RealmB).GetAsync("at-shared", default));
        Assert.NotNull(await harness.Storage.GetUserSessionStore(harness.RealmB).FindByIdAsync("sid-shared"));
    }

    /// <summary>SQLite runs this suite unconditionally; it is the baseline the other provider must match.</summary>
    public sealed class Sqlite : OperationalPurgeRealmTests
    {
        private protected override Task<IOperationalParityHarness> CreateHarnessAsync(
            IAuthorizeParametersHandleGenerator? handleGenerator = null,
            Action<OperationalCleanupOptions>? cleanup = null)
            => SqliteParityHarness.CreateAsync(handleGenerator, cleanup);
    }
}

/// <summary>
/// The same suite over PostgreSQL. The concrete suite stays private so xUnit does not discover its scenarios
/// when the opt-in connection is unavailable.
/// </summary>
public class PostgreSqlPurgeRealmTests
{
    [Tests.Storage.Configuration.StoragePostgreSqlFact]
    [Trait("Category", "PostgreSql")]
    public Task PurgeRealm()
        => Tests.Storage.Configuration.Support.ProviderFactRunner.RunAsync(new PostgreSqlSuite());

    private sealed class PostgreSqlSuite : OperationalPurgeRealmTests
    {
        private protected override Task<IOperationalParityHarness> CreateHarnessAsync(
            IAuthorizeParametersHandleGenerator? handleGenerator = null,
            Action<OperationalCleanupOptions>? cleanup = null)
            => PostgreSqlParityHarness.CreateAsync(handleGenerator, cleanup);
    }
}
