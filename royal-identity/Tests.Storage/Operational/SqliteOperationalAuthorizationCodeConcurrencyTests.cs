using System.Security.Claims;
using RoyalIdentity.Contracts.Storage;
using RoyalIdentity.Models;
using RoyalIdentity.Models.Scopes;
using RoyalIdentity.Models.Tokens;
using Tests.Storage.Operational.Support;

namespace Tests.Storage.Operational;

/// <summary>
/// The single-use acceptance of MP-2 (plan Fase 4, DF11): concurrent consumers of the same authorization code
/// produce exactly one success. This is the acceptance the transitional in-memory fake cannot satisfy and never
/// claims to (DF39) — it runs only against EF.
/// <para>
/// Every consumer has its own scope, its own <c>DbContext</c> and — pooling being off on a file-backed database
/// — its own connection, released together by an async start barrier.
/// </para>
/// </summary>
public class SqliteOperationalAuthorizationCodeConcurrencyTests
{
    private static readonly DateTime Start = Tests.Storage.Support.StorageContractHarness.Start;

    private const string ClientId = "client-race";
    private const string RedirectUri = "https://client.contract.test/callback";

    private static AuthorizationCode NewCode(Realm realm)
    {
        var subject = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "subject-race")], "contract"));

        return new AuthorizationCode(ClientId, subject, "session-state", Start, 300,
            new RequestedResources(), RedirectUri)
        {
            RealmId = realm.Id,
        };
    }

    private static async Task<AuthorizationCode> SeedCodeAsync(
        SqliteOperationalFileDatabase database, Realm realm)
    {
        var code = NewCode(realm);
        await using var scope = database.CreateScope();
        await database.StoresOf(scope).GetAuthorizationCodeStore(realm)
            .StoreAuthorizationCodeAsync(code, default);

        return code;
    }

    // The acceptance: N consumers, one code, exactly one gets it. Everyone else sees the same null a stranger
    // would, and the code is gone afterwards.
    [Theory]
    [InlineData(2)]
    [InlineData(8)]
    public async Task ConcurrentConsumers_ProduceExactlyOneSuccess(int consumers)
    {
        await using var database = await SqliteOperationalFileDatabase.CreateMigratedAsync();
        var realm = SqliteOperationalFileDatabase.NewRealm();
        var code = await SeedCodeAsync(database, realm);

        var results = new AuthorizationCode?[consumers];
        await SqliteOperationalFileDatabase.RunTogetherAsync(consumers, async (index, ready, release) =>
        {
            await using var scope = database.CreateScope();
            var store = database.StoresOf(scope).GetAuthorizationCodeStore(realm);

            await store.GetAuthorizationCodeAsync(code.Code, default);
            ready.SetResult();
            await release;

            results[index] = await store.ConsumeAuthorizationCodeAsync(
                code.Code, ClientId, RedirectUri, default);
        });

        var winner = Assert.Single(results, result => result is not null);
        Assert.Equal(code.Code, winner!.Code);
        Assert.Equal(0, await database.CountAsync("protocol_artifacts"));
    }

    // A concurrent request with the wrong binding must not be able to deny the legitimate one: it consumes
    // nothing, so the rightful consumer still gets the code.
    [Fact]
    public async Task AConcurrentMismatchedBinding_DoesNotDenyTheLegitimateConsumer()
    {
        await using var database = await SqliteOperationalFileDatabase.CreateMigratedAsync();
        var realm = SqliteOperationalFileDatabase.NewRealm();
        var code = await SeedCodeAsync(database, realm);

        AuthorizationCode? legitimate = null;
        var impostorResults = new AuthorizationCode?[6];

        await SqliteOperationalFileDatabase.RunTogetherAsync(impostorResults.Length + 1, async (index, ready, release) =>
        {
            await using var scope = database.CreateScope();
            var store = database.StoresOf(scope).GetAuthorizationCodeStore(realm);

            await store.GetAuthorizationCodeAsync(code.Code, default);
            ready.SetResult();
            await release;

            if (index is 0)
            {
                legitimate = await store.ConsumeAuthorizationCodeAsync(code.Code, ClientId, RedirectUri, default);
                return;
            }

            impostorResults[index - 1] = await store.ConsumeAuthorizationCodeAsync(
                code.Code, "client-impostor", "https://attacker.example/callback", default);
        });

        Assert.NotNull(legitimate);
        Assert.All(impostorResults, result => Assert.Null(result));
    }

    // Codes are independent: contention on one must not consume another.
    [Fact]
    public async Task ConcurrentConsumersOfDifferentCodes_AllSucceed()
    {
        await using var database = await SqliteOperationalFileDatabase.CreateMigratedAsync();
        var realm = SqliteOperationalFileDatabase.NewRealm();
        const int codes = 6;

        var handles = new AuthorizationCode[codes];
        for (var index = 0; index < codes; index++)
            handles[index] = await SeedCodeAsync(database, realm);

        var results = new AuthorizationCode?[codes];
        await SqliteOperationalFileDatabase.RunTogetherAsync(codes, async (index, ready, release) =>
        {
            await using var scope = database.CreateScope();
            var store = database.StoresOf(scope).GetAuthorizationCodeStore(realm);

            await store.GetAuthorizationCodeAsync(handles[index].Code, default);
            ready.SetResult();
            await release;

            results[index] = await store.ConsumeAuthorizationCodeAsync(
                handles[index].Code, ClientId, RedirectUri, default);
        });

        Assert.All(results, result => Assert.NotNull(result));
        Assert.Equal(0, await database.CountAsync("protocol_artifacts"));
    }

    // MP-2 belongs to the store the EF factory returns: the capability is a compile-time guarantee (DF46), so
    // the transitional fallback is unreachable from this adapter.
    [Fact]
    public async Task TheEfCodeStore_AlwaysCarriesTheSingleUseCapability()
    {
        await using var database = await SqliteOperationalFileDatabase.CreateMigratedAsync();
        var realm = SqliteOperationalFileDatabase.NewRealm();

        await using var scope = database.CreateScope();
        var store = database.StoresOf(scope).GetAuthorizationCodeStore(realm);

        Assert.IsAssignableFrom<ISingleUseAuthorizationCodeStore>(store);
        Assert.IsAssignableFrom<IAuthorizationCodeStore>(store);
    }
}
