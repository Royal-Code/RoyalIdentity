using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using RoyalIdentity.Contracts.Storage;
using RoyalIdentity.Models;
using RoyalIdentity.Models.Scopes;
using RoyalIdentity.Options;
using RoyalIdentity.Responses.HttpResults;
using Tests.Integration.Prepare;
using RealmModel = RoyalIdentity.Models.Realm;

namespace Tests.Integration.Endpoints;

/// <summary>
/// Flow-level behavior of the refresh grant after Fase 5 (plan-data-operational-storage DF32/DF41/DF42): the
/// realm's <c>ClaimsMode</c> is the only policy — the per-client flag is gone — the renewal never depends on the
/// row of the access token issued alongside, and the identity token's <c>at_hash</c> covers the access token
/// returned in the same response.
/// </summary>
[Collection(RefreshTokenClaimsModeCollection.Name)]
public class RefreshTokenClaimsModeTests : IClassFixture<AppFactory>
{
    private readonly AppFactory factory;

    public RefreshTokenClaimsModeTests(AppFactory factory) => this.factory = factory;

    private string RegisterClient(
        string clientId,
        RealmModel? realm = null,
        Action<Client>? configure = null)
    {
        realm ??= MemoryStorage.DemoRealm;
        var storage = factory.Services.GetRequiredService<MemoryStorage>();
        var client = new Client
        {
            Realm = realm,
            Id = clientId,
            Name = "Refresh Client",
            RequireClientSecret = false,
            AllowOfflineAccess = true,
            AllowedIdentityScopes = { "openid", "profile", "email" },
            AllowedResponseTypes = { "code" },
            AllowedGrantTypes = ["code", "refresh_token"],
            RedirectUris = { "http://localhost:5000/**", "https://localhost:5001/**" },
        };
        configure?.Invoke(client);
        storage.GetRealmMemoryStore(realm).Clients.TryAdd(clientId, client);

        return clientId;
    }

    private static Task<HttpResponseMessage> RefreshResponseAsync(
        HttpClient client,
        string refreshToken,
        string clientId,
        RealmModel? realm = null,
        string? resource = null)
    {
        realm ??= MemoryStorage.DemoRealm;
        var parameters = new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = refreshToken,
            ["client_id"] = clientId,
        };
        if (resource is not null)
            parameters["resource"] = resource;

        return client.PostAsync(
            Oidc.Routes.BuildTokenUrl(realm.Path),
            new FormUrlEncodedContent(parameters));
    }

    private static async Task<Dictionary<string, object>> RefreshAsync(
        HttpClient client, string refreshToken, string clientId, RealmModel? realm = null)
    {
        var response = await RefreshResponseAsync(client, refreshToken, clientId, realm);
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());

        return (await response.Content.ReadFromJsonAsync<Dictionary<string, object>>())!;
    }

    private static Task AssertInvalidGrantAsync(HttpResponseMessage response)
        => AssertProtocolErrorAsync(response, "invalid_grant");

    private static async Task AssertProtocolErrorAsync(HttpResponseMessage response, string error)
    {
        var raw = await response.Content.ReadAsStringAsync();
        var body = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(raw);

        Assert.True(
            response.StatusCode == HttpStatusCode.BadRequest,
            $"Expected BadRequest but received {response.StatusCode}: {raw}");
        Assert.NotNull(body);
        Assert.Equal(error, body!["error"].GetString());
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
    private IDisposable AddUserClaim(string type, string value, RealmModel? realm = null)
    {
        realm ??= MemoryStorage.DemoRealm;
        var storage = factory.Services.GetRequiredService<MemoryStorage>();
        var alice = storage.GetRealmMemoryStore(realm).UserAccounts.Values
            .Single(user => user.SubjectId == MemoryStorage.AliceSubjectId);
        var claim = new Claim(type, value);
        alice.Claims.Add(claim);

        return new Revert(() => alice.Claims.Remove(claim));
    }

    private IDisposable SetAliceActive(bool active, RealmModel? realm = null)
    {
        realm ??= MemoryStorage.DemoRealm;
        var storage = factory.Services.GetRequiredService<MemoryStorage>();
        var alice = storage.GetRealmMemoryStore(realm).UserAccounts.Values
            .Single(user => user.SubjectId == MemoryStorage.AliceSubjectId);
        var previous = alice.IsActive;
        alice.IsActive = active;

        return new Revert(() => alice.IsActive = previous);
    }

    private IDisposable SeedAliceInServerRealm()
    {
        var storage = factory.Services.GetRequiredService<MemoryStorage>();
        var demoAlice = storage.GetDemoRealmStore().UserAccounts["alice"];
        var serverUsers = storage.GetServerRealmStore().UserAccounts;
        serverUsers.TryGetValue("alice", out var previous);
        serverUsers["alice"] = new MemoryUserAccount
        {
            SubjectId = demoAlice.SubjectId,
            Username = demoAlice.Username,
            PasswordHash = demoAlice.PasswordHash,
            DisplayName = demoAlice.DisplayName,
            IsActive = demoAlice.IsActive,
            Claims = [.. demoAlice.Claims.Select(claim => new Claim(claim.Type, claim.Value, claim.ValueType))],
        };

        return new Revert(() =>
        {
            if (previous is null)
                serverUsers.TryRemove("alice", out _);
            else
                serverUsers["alice"] = previous;
        });
    }

    private sealed class Revert(Action revert) : IDisposable
    {
        public void Dispose() => revert();
    }

    private IDisposable UseClaimsMode(RefreshTokenClaimsMode mode, RealmModel? realm = null)
    {
        realm ??= MemoryStorage.DemoRealm;
        var previous = realm.Options.RefreshTokens.ClaimsMode;
        realm.Options.RefreshTokens.ClaimsMode = mode;

        return new Revert(() => realm.Options.RefreshTokens.ClaimsMode = previous);
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
        var accessToken = ReadJwtPayload(refreshed["access_token"].ToString()!);
        var identityToken = ReadJwtPayload(refreshed["id_token"].ToString()!);

        Assert.Equal("https://alice.example", accessToken.GetProperty("website").GetString());
        Assert.Equal("https://alice.example", identityToken.GetProperty("website").GetString());
        // The grant itself is unchanged: Current narrows or keeps, never widens.
        Assert.Equal(
            ReadJwtPayload(issued.AccessToken!).GetProperty("scope").ToString(),
            accessToken.GetProperty("scope").ToString());
    }

    [Fact]
    public async Task CurrentMode_ReflectsAClaimRemovedAfterTheGrant_InBothTokens()
    {
        var clientId = RegisterClient("refresh_current_removed_claim_client");
        using var mode = UseClaimsMode(RefreshTokenClaimsMode.Current);

        var client = factory.CreateClient();
        await client.LoginAliceAsync();

        TokenEndpointParameters issued;
        using (AddUserClaim("website", "https://removed-before-refresh.example"))
        {
            issued = await client.GetTokensAsync(clientId: clientId);
            Assert.Equal(
                "https://removed-before-refresh.example",
                ReadJwtPayload(issued.AccessToken!).GetProperty("website").GetString());
            Assert.Equal(
                "https://removed-before-refresh.example",
                ReadJwtPayload(issued.IdentityToken!).GetProperty("website").GetString());
        }

        var refreshed = await RefreshAsync(client, issued.RefreshToken!, clientId);

        Assert.False(ReadJwtPayload(refreshed["access_token"].ToString()!).TryGetProperty("website", out _));
        Assert.False(ReadJwtPayload(refreshed["id_token"].ToString()!).TryGetProperty("website", out _));
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

    [Fact]
    public async Task SnapshotMode_PreservesTheOriginalClaims_WithoutCopyingAccessOnlyClaimsIntoTheIdentityToken()
    {
        var clientId = RegisterClient(
            "refresh_snapshot_distinct_claim_sets_client",
            configure: client =>
            {
                client.ClientClaimsPrefix = string.Empty;
                client.Claims.Add(new Claim("access_only", "client-value"));
            });
        using var mode = UseClaimsMode(RefreshTokenClaimsMode.Snapshot);

        var client = factory.CreateClient();
        await client.LoginAliceAsync();

        TokenEndpointParameters issued;
        using (AddUserClaim("website", "https://snapshot.example"))
            issued = await client.GetTokensAsync(clientId: clientId);

        var refreshed = await RefreshAsync(client, issued.RefreshToken!, clientId);
        var accessToken = ReadJwtPayload(refreshed["access_token"].ToString()!);
        var identityToken = ReadJwtPayload(refreshed["id_token"].ToString()!);

        Assert.Equal("https://snapshot.example", accessToken.GetProperty("website").GetString());
        Assert.Equal("https://snapshot.example", identityToken.GetProperty("website").GetString());
        Assert.Equal(clientId, accessToken.GetProperty("client_id").GetString());
        Assert.Equal("client-value", accessToken.GetProperty("access_only").GetString());
        Assert.False(identityToken.TryGetProperty("client_id", out _));
        Assert.False(identityToken.TryGetProperty("access_only", out _));
    }

    // DF32 realm isolation: two simultaneously configured realms must apply their own policies over the same
    // user/profile change. Switching one global realm sequentially would not prove this boundary.
    [Fact]
    public async Task TwoRealms_WithDifferentModes_DoNotShareClaimsPolicy()
    {
        using var serverAlice = SeedAliceInServerRealm();
        using var currentMode = UseClaimsMode(RefreshTokenClaimsMode.Current, MemoryStorage.DemoRealm);
        using var snapshotMode = UseClaimsMode(RefreshTokenClaimsMode.Snapshot, MemoryStorage.ServerRealm);
        var currentClientId = RegisterClient("refresh_two_realms_current", MemoryStorage.DemoRealm);
        var snapshotClientId = RegisterClient("refresh_two_realms_snapshot", MemoryStorage.ServerRealm);

        var currentClient = factory.CreateClient();
        await currentClient.LoginAliceAsync();
        var currentIssued = await currentClient.GetTokensAsync(clientId: currentClientId);

        var snapshotClient = factory.CreateClient();
        await snapshotClient.LoginAsync("alice", "alice", MemoryStorage.ServerRealm.Path);
        var snapshotIssued = await snapshotClient.GetTokensAsync(
            snapshotClientId, "openid profile offline_access", MemoryStorage.ServerRealm.Path);

        using var demoClaim = AddUserClaim(
            "website", "https://current-realm.example", MemoryStorage.DemoRealm);
        using var serverClaim = AddUserClaim(
            "website", "https://snapshot-realm.example", MemoryStorage.ServerRealm);

        var currentRefreshed = await RefreshAsync(
            currentClient, currentIssued.RefreshToken!, currentClientId, MemoryStorage.DemoRealm);
        var snapshotRefreshed = await RefreshAsync(
            snapshotClient, snapshotIssued.RefreshToken!, snapshotClientId, MemoryStorage.ServerRealm);

        Assert.Equal(
            "https://current-realm.example",
            ReadJwtPayload(currentRefreshed["access_token"].ToString()!).GetProperty("website").GetString());
        Assert.False(
            ReadJwtPayload(snapshotRefreshed["access_token"].ToString()!).TryGetProperty("website", out _));
    }

    // The default is Current, with no client option able to interfere — the per-client flag no longer exists.
    [Fact]
    public async Task ConsumedToken_WithZeroTolerance_IsRejected()
    {
        var clientId = RegisterClient(
            "refresh_zero_tolerance_client",
            configure: client => client.RefreshTokenPostConsumedTimeTolerance = TimeSpan.Zero);
        var client = factory.CreateClient();
        await client.LoginAliceAsync();
        var issued = await client.GetTokensAsync(clientId: clientId);

        await RefreshAsync(client, issued.RefreshToken!, clientId);
        await AssertInvalidGrantAsync(await RefreshResponseAsync(client, issued.RefreshToken!, clientId));
    }

    [Fact]
    public async Task ConsumedToken_WithinFiniteTolerance_IsAccepted()
    {
        var clientId = RegisterClient(
            "refresh_finite_tolerance_client",
            configure: client => client.RefreshTokenPostConsumedTimeTolerance = TimeSpan.FromMinutes(5));
        var client = factory.CreateClient();
        await client.LoginAliceAsync();
        var issued = await client.GetTokensAsync(clientId: clientId);

        await RefreshAsync(client, issued.RefreshToken!, clientId);
        var repeated = await RefreshResponseAsync(client, issued.RefreshToken!, clientId);

        Assert.Equal(HttpStatusCode.OK, repeated.StatusCode);
    }

    [Fact]
    public async Task ConsumedToken_OutsideFiniteTolerance_IsRejectedUsingThePersistedTimestamp()
    {
        var clientId = RegisterClient(
            "refresh_expired_finite_tolerance_client",
            configure: client => client.RefreshTokenPostConsumedTimeTolerance = TimeSpan.FromMinutes(5));
        var client = factory.CreateClient();
        await client.LoginAliceAsync();
        var issued = await client.GetTokensAsync(clientId: clientId);
        await RefreshAsync(client, issued.RefreshToken!, clientId);

        var store = factory.Services.GetRequiredService<IStorage>()
            .GetRefreshTokenStore(MemoryStorage.DemoRealm);
        var consumed = await store.GetAsync(issued.RefreshToken!, default);
        Assert.NotNull(consumed);
        Assert.NotNull(consumed!.ConsumedTime);
        consumed.ConsumedTime = consumed.ConsumedTime.Value.AddMinutes(-6);
        await store.UpdateAsync(consumed, default);

        await AssertInvalidGrantAsync(await RefreshResponseAsync(client, issued.RefreshToken!, clientId));
    }

    [Fact]
    public async Task ConsumedToken_WithInfiniteTolerance_IsAccepted()
    {
        var clientId = RegisterClient(
            "refresh_infinite_tolerance_client",
            configure: client =>
            {
                client.RefreshTokenExpiration = TokenExpiration.Absolute;
                client.RefreshTokenPostConsumedTimeTolerance = TimeSpan.MaxValue;
            });
        var client = factory.CreateClient();
        await client.LoginAliceAsync();
        var issued = await client.GetTokensAsync(clientId: clientId);

        await RefreshAsync(client, issued.RefreshToken!, clientId);
        var repeated = await RefreshResponseAsync(client, issued.RefreshToken!, clientId);

        Assert.Equal(HttpStatusCode.OK, repeated.StatusCode);
    }

    [Theory]
    [InlineData(RefreshTokenClaimsMode.Current)]
    [InlineData(RefreshTokenClaimsMode.Snapshot)]
    public async Task BothClaimsModes_RejectAnInactiveSubject(RefreshTokenClaimsMode claimsMode)
    {
        var clientId = RegisterClient($"refresh_inactive_{claimsMode.ToString().ToLowerInvariant()}");
        using var mode = UseClaimsMode(claimsMode);
        var client = factory.CreateClient();
        await client.LoginAliceAsync();
        var issued = await client.GetTokensAsync(clientId: clientId);

        using var inactive = SetAliceActive(false);
        await AssertInvalidGrantAsync(await RefreshResponseAsync(client, issued.RefreshToken!, clientId));
    }

    [Theory]
    [InlineData(RefreshTokenClaimsMode.Current)]
    [InlineData(RefreshTokenClaimsMode.Snapshot)]
    public async Task BothClaimsModes_RejectAnEndedSession(RefreshTokenClaimsMode claimsMode)
    {
        var clientId = RegisterClient($"refresh_ended_session_{claimsMode.ToString().ToLowerInvariant()}");
        using var mode = UseClaimsMode(claimsMode);
        var client = factory.CreateClient();
        await client.LoginAliceAsync();
        var issued = await client.GetTokensAsync(clientId: clientId);
        var storage = factory.Services.GetRequiredService<IStorage>();
        var refreshToken = await storage.GetRefreshTokenStore(MemoryStorage.DemoRealm)
            .GetAsync(issued.RefreshToken!, default);
        Assert.NotNull(refreshToken);
        Assert.NotNull(refreshToken!.SessionId);
        await storage.GetUserSessionStore(MemoryStorage.DemoRealm)
            .EndAsync(refreshToken.SessionId!, default);

        await AssertInvalidGrantAsync(await RefreshResponseAsync(client, issued.RefreshToken!, clientId));
    }

    [Fact]
    public async Task InvalidTargetAfterTheCas_LeavesTheRefreshTokenConsumed()
    {
        var clientId = RegisterClient(
            "refresh_invalid_target_after_cas_client",
            configure: client =>
            {
                client.AllowedResourceServers.Add("apiserver");
                client.RefreshTokenPostConsumedTimeTolerance = TimeSpan.Zero;
            });
        var client = factory.CreateClient();
        await client.LoginAliceAsync();
        var issued = await client.GetTokensAsync(
            clientId, "openid profile offline_access api");
        var storage = factory.Services.GetRequiredService<MemoryStorage>();
        var original = await storage.GetRefreshTokenStore(MemoryStorage.DemoRealm)
            .GetAsync(issued.RefreshToken!, default);
        Assert.NotNull(original);
        original!.ResourceUris.Add("https://api.demo.local/apiserver");
        await storage.GetRefreshTokenStore(MemoryStorage.DemoRealm).UpdateAsync(original, default);
        var resourceServerName = $"refresh-unauthorized-{Guid.NewGuid():N}";
        var unauthorizedResource = $"https://unauthorized.example/{Guid.NewGuid():N}";
        storage.GetDemoRealmStore().ResourceServers[resourceServerName] = new ResourceServer(
            ScopeVisibility.Public,
            resourceServerName,
            "Unauthorized resource",
            "Known by the realm but outside the original grant")
        {
            ProtectedResources = [new ProtectedResource(unauthorizedResource)],
        };
        using var resource = new Revert(
            () => storage.GetDemoRealmStore().ResourceServers.TryRemove(resourceServerName, out _));

        var invalidTarget = await RefreshResponseAsync(
            client,
            issued.RefreshToken!,
            clientId,
            resource: unauthorizedResource);
        await AssertProtocolErrorAsync(invalidTarget, "invalid_target");

        var persisted = await factory.Services.GetRequiredService<IStorage>()
            .GetRefreshTokenStore(MemoryStorage.DemoRealm)
            .GetAsync(issued.RefreshToken!, default);
        Assert.NotNull(persisted?.ConsumedTime);

        await AssertInvalidGrantAsync(await RefreshResponseAsync(client, issued.RefreshToken!, clientId));
    }

    [Fact]
    public void ClaimsMode_DefaultsToCurrent_AndHasNoClientOverride()
    {
        Assert.Equal(
            RefreshTokenClaimsMode.Current,
            new RealmOptions(new ServerOptions()).RefreshTokens.ClaimsMode);

        Assert.Null(typeof(Client).GetProperty("UpdateAccessTokenClaimsOnRefresh"));
    }
}

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class RefreshTokenClaimsModeCollection
{
    public const string Name = nameof(RefreshTokenClaimsModeCollection);
}
