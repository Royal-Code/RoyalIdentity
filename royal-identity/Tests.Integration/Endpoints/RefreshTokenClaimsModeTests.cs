using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using RoyalIdentity.Contracts.Storage;
using RoyalIdentity.Models;
using RoyalIdentity.Options;
using Tests.Integration.Prepare;

namespace Tests.Integration.Endpoints;

/// <summary>
/// Flow-level behavior of the refresh grant after Fase 5 (plan-data-operational-storage DF32/DF41/DF42): the
/// realm's <c>ClaimsMode</c> is the only policy — the per-client flag is gone — the renewal never depends on the
/// row of the access token issued alongside, and the identity token's <c>at_hash</c> covers the access token
/// returned in the same response.
/// </summary>
public class RefreshTokenClaimsModeTests : IClassFixture<AppFactory>
{
    private readonly AppFactory factory;

    public RefreshTokenClaimsModeTests(AppFactory factory) => this.factory = factory;

    private static string TokenUrl => Oidc.Routes.BuildTokenUrl(MemoryStorage.DemoRealm.Path);

    private string RegisterClient(string clientId)
    {
        var storage = factory.Services.GetRequiredService<MemoryStorage>();
        storage.GetDemoRealmStore().Clients.TryAdd(clientId, new Client
        {
            Realm = MemoryStorage.DemoRealm,
            Id = clientId,
            Name = "Refresh Client",
            RequireClientSecret = false,
            AllowOfflineAccess = true,
            AllowedIdentityScopes = { "openid", "profile", "email" },
            AllowedResponseTypes = { "code" },
            AllowedGrantTypes = ["code", "refresh_token"],
            RedirectUris = { "http://localhost:5000/**", "https://localhost:5001/**" },
        });

        return clientId;
    }

    private static async Task<Dictionary<string, object>> RefreshAsync(
        HttpClient client, string refreshToken, string clientId)
    {
        var response = await client.PostAsync(TokenUrl, new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = refreshToken,
            ["client_id"] = clientId,
        }));

        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());

        return (await response.Content.ReadFromJsonAsync<Dictionary<string, object>>())!;
    }

    private static JsonElement ReadJwtPayload(string jwt)
    {
        var payload = jwt.Split('.')[1];
        var padded = payload.PadRight(payload.Length + ((4 - (payload.Length % 4)) % 4), '=')
            .Replace('-', '+')
            .Replace('_', '/');

        return JsonDocument.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(padded))).RootElement;
    }

    /// <summary>The OIDC at_hash: left half of the SHA-256 of the ASCII token, base64url.</summary>
    private static string ComputeAtHash(string accessToken)
    {
        var hash = SHA256.HashData(Encoding.ASCII.GetBytes(accessToken));

        return Convert.ToBase64String(hash, 0, hash.Length / 2)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }

    // DF42: the identity token issued on a refresh must hash the access token returned with it. Hashing the
    // previous one produced an id_token that did not match the response the client received.
    [Fact]
    public async Task Refresh_IdentityToken_HashesTheNewAccessToken()
    {
        var clientId = RegisterClient("refresh_at_hash_client");
        var client = factory.CreateClient();
        await client.LoginAliceAsync();
        var issued = await client.GetTokensAsync(clientId: clientId);

        var refreshed = await RefreshAsync(client, issued.RefreshToken!, clientId);

        var newAccessToken = refreshed["access_token"].ToString()!;
        var identityToken = refreshed["id_token"].ToString()!;
        var atHash = ReadJwtPayload(identityToken).GetProperty("at_hash").GetString();

        Assert.NotEqual(issued.AccessToken, newAccessToken);
        Assert.Equal(ComputeAtHash(newAccessToken), atHash);
        // ...and demonstrably not the previous one, which is what the old implementation hashed.
        Assert.NotEqual(ComputeAtHash(issued.AccessToken!), atHash);
    }

    // DF41: the refresh carries its own grant. Removing every access token of the realm — as cleanup would —
    // must not break a renewal.
    [Fact]
    public async Task Refresh_StillWorks_AfterThePreviousAccessTokenIsGone()
    {
        var clientId = RegisterClient("refresh_no_access_token_client");
        var client = factory.CreateClient();
        await client.LoginAliceAsync();
        var issued = await client.GetTokensAsync(clientId: clientId);

        var storage = factory.Services.GetRequiredService<MemoryStorage>();
        storage.GetDemoRealmStore().AccessTokens.Clear();

        var refreshed = await RefreshAsync(client, issued.RefreshToken!, clientId);

        Assert.NotNull(refreshed["access_token"].ToString());
    }

    // DF41: no jti of a previous access token is persisted on the refresh token, and none is rewritten onto a
    // reusable one.
    [Fact]
    public async Task IssuedRefreshToken_CarriesNoAccessTokenIdentifier()
    {
        var clientId = RegisterClient("refresh_no_jti_client");
        var client = factory.CreateClient();
        await client.LoginAliceAsync();
        var issued = await client.GetTokensAsync(clientId: clientId);

        var storage = factory.Services.GetRequiredService<IStorage>();
        var stored = await storage.GetRefreshTokenStore(MemoryStorage.DemoRealm)
            .GetAsync(issued.RefreshToken!, default);

        Assert.NotNull(stored);
        Assert.DoesNotContain(stored.Claims, claim => claim.Type == "jti");
    }

    /// <summary>Changes Alice's profile between the grant and the renewal, which is what tells the modes apart.</summary>
    private IDisposable AddUserClaim(string type, string value)
    {
        var storage = factory.Services.GetRequiredService<MemoryStorage>();
        var alice = storage.GetDemoRealmStore().UserAccounts.Values
            .Single(user => user.SubjectId == MemoryStorage.AliceSubjectId);
        var claim = new System.Security.Claims.Claim(type, value);
        alice.Claims.Add(claim);

        return new Revert(() => alice.Claims.Remove(claim));
    }

    private sealed class Revert(Action revert) : IDisposable
    {
        public void Dispose() => revert();
    }

    private IDisposable UseClaimsMode(RefreshTokenClaimsMode mode)
    {
        var previous = MemoryStorage.DemoRealm.Options.RefreshTokens.ClaimsMode;
        MemoryStorage.DemoRealm.Options.RefreshTokens.ClaimsMode = mode;

        return new Revert(() => MemoryStorage.DemoRealm.Options.RefreshTokens.ClaimsMode = previous);
    }

    // DF32: Current re-runs issuance against the claims the provider gives now, so a claim added after the grant
    // appears in the renewed token — in both the access token and the identity token of the same response.
    [Fact]
    public async Task CurrentMode_ReflectsAClaimAddedAfterTheGrant()
    {
        var clientId = RegisterClient("refresh_current_mode_client");
        using var mode = UseClaimsMode(RefreshTokenClaimsMode.Current);

        var client = factory.CreateClient();
        await client.LoginAliceAsync();
        var issued = await client.GetTokensAsync(clientId: clientId);
        Assert.False(ReadJwtPayload(issued.AccessToken!).TryGetProperty("website", out _));

        using var claim = AddUserClaim("website", "https://alice.example");
        var refreshed = await RefreshAsync(client, issued.RefreshToken!, clientId);
        var after = ReadJwtPayload(refreshed["access_token"].ToString()!);

        Assert.Equal("https://alice.example", after.GetProperty("website").GetString());
        // The grant itself is unchanged: Current narrows or keeps, never widens.
        Assert.Equal(
            ReadJwtPayload(issued.AccessToken!).GetProperty("scope").ToString(),
            after.GetProperty("scope").ToString());
    }

    // DF32: Snapshot reproduces the claims of the grant, so a claim added afterwards must NOT leak in — and
    // that has to hold for the identity token too, or the two tokens of one response would disagree.
    [Fact]
    public async Task SnapshotMode_IgnoresAClaimAddedAfterTheGrant_InBothTokens()
    {
        var clientId = RegisterClient("refresh_snapshot_mode_client");
        using var mode = UseClaimsMode(RefreshTokenClaimsMode.Snapshot);

        var client = factory.CreateClient();
        await client.LoginAliceAsync();
        var issued = await client.GetTokensAsync(clientId: clientId);

        using var claim = AddUserClaim("website", "https://added-after.example");
        var refreshed = await RefreshAsync(client, issued.RefreshToken!, clientId);

        var accessToken = ReadJwtPayload(refreshed["access_token"].ToString()!);
        var identityToken = ReadJwtPayload(refreshed["id_token"].ToString()!);

        Assert.False(accessToken.TryGetProperty("website", out _));
        Assert.False(identityToken.TryGetProperty("website", out _));
        // Still a fresh token instance, not a replay of the old one.
        Assert.NotEqual(
            ReadJwtPayload(issued.AccessToken!).GetProperty("jti").GetString(),
            accessToken.GetProperty("jti").GetString());
    }

    // The two modes must be observably different over the very same scenario.
    [Fact]
    public async Task TheTwoModes_DisagreeOnAClaimAddedAfterTheGrant()
    {
        var currentClient = RegisterClient("refresh_mode_diff_current");
        var snapshotClient = RegisterClient("refresh_mode_diff_snapshot");
        var client = factory.CreateClient();
        await client.LoginAliceAsync();

        string? currentValue;
        string? snapshotValue;

        using (UseClaimsMode(RefreshTokenClaimsMode.Snapshot))
        {
            var issued = await client.GetTokensAsync(clientId: snapshotClient);
            using var claim = AddUserClaim("website", "https://only-current-sees-this.example");
            var refreshed = await RefreshAsync(client, issued.RefreshToken!, snapshotClient);
            snapshotValue = ReadJwtPayload(refreshed["access_token"].ToString()!)
                .TryGetProperty("website", out var snapshotWebsite) ? snapshotWebsite.GetString() : null;
        }

        using (UseClaimsMode(RefreshTokenClaimsMode.Current))
        {
            var issued = await client.GetTokensAsync(clientId: currentClient);
            using var claim = AddUserClaim("website", "https://only-current-sees-this.example");
            var refreshed = await RefreshAsync(client, issued.RefreshToken!, currentClient);
            currentValue = ReadJwtPayload(refreshed["access_token"].ToString()!)
                .TryGetProperty("website", out var currentWebsite) ? currentWebsite.GetString() : null;
        }

        Assert.Equal("https://only-current-sees-this.example", currentValue);
        Assert.Null(snapshotValue);
    }

    // The default is Current, with no client option able to interfere — the per-client flag no longer exists.
    [Fact]
    public void ClaimsMode_DefaultsToCurrent_AndHasNoClientOverride()
    {
        Assert.Equal(
            RefreshTokenClaimsMode.Current,
            new RealmOptions(new ServerOptions()).RefreshTokens.ClaimsMode);

        Assert.Null(typeof(Client).GetProperty("UpdateAccessTokenClaimsOnRefresh"));
    }
}
