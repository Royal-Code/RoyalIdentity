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

namespace Tests.Storage.Operational;

/// <summary>
/// The Operational half of the realm deletion (plan Fase 6, MP-7/DF18): the purge removes every Operational
/// record of one realm, is repeatable, and never reaches Configuration — the realm tombstone and its
/// path/domain reservation live there and must survive.
/// </summary>
public class SqliteOperationalPurgeRealmTests
{
    private static readonly DateTime Start = Tests.Storage.Support.StorageContractHarness.Start;

    private static IOperationalMaintenance Maintenance(SqliteOperationalStorageHarness harness)
        => harness.ScopedServices.GetRequiredService<IOperationalMaintenance>();

    /// <summary>Writes one row into every Operational table of a realm.</summary>
    private static async Task SeedEveryTableAsync(SqliteOperationalStorageHarness harness, Realm realm, string suffix)
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
                "session-state", Start, 300, new RequestedResources(), "https://client.contract.test/callback")
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

    private static async Task<int> CountAsync<TEntity>(SqliteOperationalStorageHarness harness, string realmId)
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
        await using var harness = await SqliteOperationalStorageHarness.CreateConcreteAsync();
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
        await using var harness = await SqliteOperationalStorageHarness.CreateConcreteAsync();
        await SeedEveryTableAsync(harness, harness.RealmA, "a");

        await Maintenance(harness).PurgeRealmAsync(harness.RealmA.Id);

        await using var context = harness.NewOperationalContext();
        // The Configuration tables live in the same SQLite file in this fixture, which makes the assertion real
        // rather than a tautology of separate databases.
        var realms = await harness.DbContext.Set<RealmEntity>().AsNoTracking().CountAsync();
        var clients = await harness.DbContext.Set<ClientEntity>().AsNoTracking().CountAsync();

        Assert.True(realms > 0, "the realm rows must survive the Operational purge");
        Assert.NotNull(await harness.Storage.Realms.GetByIdAsync(harness.RealmA.Id, default));
        Assert.Equal(clients, await harness.DbContext.Set<ClientEntity>().AsNoTracking().CountAsync());
    }

    [Fact]
    public async Task Purge_IsIdempotent()
    {
        await using var harness = await SqliteOperationalStorageHarness.CreateConcreteAsync();
        await SeedEveryTableAsync(harness, harness.RealmA, "a");

        Assert.NotEqual(0, (await Maintenance(harness).PurgeRealmAsync(harness.RealmA.Id)).Total);
        Assert.Equal(0, (await Maintenance(harness).PurgeRealmAsync(harness.RealmA.Id)).Total);
        Assert.Equal(0, (await Maintenance(harness).PurgeRealmAsync("realm-that-never-existed")).Total);
    }

    // DF5: colliding ids across realms are different rows, so purging one realm cannot take the other's.
    [Fact]
    public async Task Purge_WithCollidingIdsAcrossRealms_RemovesOnlyTheTargetRealm()
    {
        await using var harness = await SqliteOperationalStorageHarness.CreateConcreteAsync();
        // The very same handles in both realms.
        await SeedEveryTableAsync(harness, harness.RealmA, "shared");
        await SeedEveryTableAsync(harness, harness.RealmB, "shared");

        await Maintenance(harness).PurgeRealmAsync(harness.RealmA.Id);

        Assert.Null(await harness.Storage.GetAccessTokenStore(harness.RealmA).GetAsync("at-shared", default));
        Assert.NotNull(await harness.Storage.GetAccessTokenStore(harness.RealmB).GetAsync("at-shared", default));
        Assert.NotNull(await harness.Storage.GetUserSessionStore(harness.RealmB).FindByIdAsync("sid-shared"));
    }
}
