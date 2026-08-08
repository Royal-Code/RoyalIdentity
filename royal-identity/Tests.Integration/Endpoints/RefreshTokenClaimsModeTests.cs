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

namespace Tests.Integration.Endpoints;

/// <summary>
/// Flow-level behavior of the refresh grant after Fase 5 (plan-data-operational-storage DF32/DF41/DF42): the
/// realm's <c>ClaimsMode</c> is the only policy — the per-client flag is gone — the renewal never depends on the
/// row of the access token issued alongside, and the identity token's <c>at_hash</c> covers the access token
/// returned in the same response.
/// </summary>
[Collection(RefreshTokenClaimsModeCollection.Name)]
public class RefreshTokenClaimsModeTests : IClassFixture<PersistentStorageAppFactory>
{
    private readonly PersistentStorageAppFactory factory;

    public RefreshTokenClaimsModeTests(PersistentStorageAppFactory factory) => this.factory = factory;

    private async Task<string> RegisterClientAsync(
        string clientId,
        TestRealmHandle? realm = null,
        Action<TestClientBuilder>? configure = null)
    {
        realm ??= factory.Handles.Demo;
        await factory.SaveClientAsync(realm, clientId, client =>
        {
            client.Name = "Refresh Client";
            client.RequireClientSecret = false;
            client.AllowOfflineAccess = true;
                client.AllowedIdentityScopes.UnionWith(["openid", "profile", "email"]);
                client.AllowedScopes.Add("api");
                client.AllowedResponseTypes.Add("code");
            client.AllowedGrantTypes.UnionWith(["code", "refresh_token"]);
            client.RedirectUris.UnionWith(["https://localhost:5000/callback", "https://localhost:5001/callback"]);
            configure?.Invoke(client);
        });

        return clientId;
    }

    private Task<HttpResponseMessage> RefreshResponseAsync(
        HttpClient client,
        string refreshToken,
        string clientId,
        TestRealmHandle? realm = null,
        string? resource = null)
    {
        realm ??= factory.Handles.Demo;
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

    private async Task<Dictionary<string, object>> RefreshAsync(
        HttpClient client, string refreshToken, string clientId, TestRealmHandle? realm = null)
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
        var clientId = await RegisterClientAsync("refresh_at_hash_client");
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
        var clientId = await RegisterClientAsync("refresh_no_access_token_client");
        var client = factory.CreateClient();
        await client.LoginAliceAsync();
        var issued = await client.GetTokensAsync(clientId: clientId);

        var realm = await factory.LoadRealmAsync(factory.Handles.Demo);
        var previousJti = ReadJwtPayload(issued.AccessToken!).GetProperty("jti").GetString();
        Assert.NotNull(previousJti);
        await factory.WithStorageAsync(
            storage => storage.GetAccessTokenStore(realm).RemoveAsync(previousJti, default));

        var refreshed = await RefreshAsync(client, issued.RefreshToken!, clientId);

        Assert.NotNull(refreshed["access_token"].ToString());
    }

    // DF41: no jti of a previous access token is persisted on the refresh token, and none is rewritten onto a
    // reusable one.
    [Fact]
    public async Task IssuedRefreshToken_CarriesNoAccessTokenIdentifier()
    {
        var clientId = await RegisterClientAsync("refresh_no_jti_client");
        var client = factory.CreateClient();
        await client.LoginAliceAsync();
        var issued = await client.GetTokensAsync(clientId: clientId);

        var realm = await factory.LoadRealmAsync(factory.Handles.Demo);
        var stored = await factory.WithStorageAsync(
            storage => storage.GetRefreshTokenStore(realm).GetAsync(issued.RefreshToken!, default));

        Assert.NotNull(stored);
        Assert.DoesNotContain(stored.Claims, claim => claim.Type == "jti");
    }

    /// <summary>Changes Alice's profile between the grant and the renewal, which is what tells the modes apart.</summary>
    private async Task<IAsyncDisposable> AddUserClaimAsync(
        string type,
        string value,
        TestRealmHandle? realm = null)
    {
        realm ??= factory.Handles.Demo;
        await factory.EnsureAccountClaimDefinitionAsync(realm, "profile", type);
        await factory.SetAccountClaimAsync(
            realm,
            factory.Handles.Alice,
            "profile",
            type,
            [value]);

        return new AsyncRevert(() => factory.SetAccountClaimAsync(
            realm,
            factory.Handles.Alice,
            "profile",
            type,
            []));
    }

    private async Task<IAsyncDisposable> SetAliceActiveAsync(
        bool active,
        TestRealmHandle? realm = null)
    {
        realm ??= factory.Handles.Demo;
        await factory.SetAccountActiveAsync(realm, factory.Handles.Alice, active);
        return new AsyncRevert(
            () => factory.SetAccountActiveAsync(realm, factory.Handles.Alice, !active));
    }

    private sealed class AsyncRevert(Func<Task> revert) : IAsyncDisposable
    {
        public ValueTask DisposeAsync() => new(revert());
    }

    private async Task<IAsyncDisposable> UseClaimsModeAsync(
        RefreshTokenClaimsMode mode,
        TestRealmHandle? realm = null)
    {
        realm ??= factory.Handles.Demo;
        var materialized = await factory.LoadRealmAsync(realm);
        var previous = materialized.Options.RefreshTokens.ClaimsMode;
        await factory.UpdateRealmAsync(
            realm,
            options => options.RefreshTokens.ClaimsMode = mode);
        return new AsyncRevert(() => factory.UpdateRealmAsync(
            realm,
            options => options.RefreshTokens.ClaimsMode = previous));
    }

    // DF32: Current re-runs issuance against the claims the provider gives now, so a claim added after the grant
    // appears in the renewed token — in both the access token and the identity token of the same response.
    [Fact]
    public async Task CurrentMode_ReflectsAClaimAddedAfterTheGrant()
    {
        var clientId = await RegisterClientAsync("refresh_current_mode_client");
        await using var mode = await UseClaimsModeAsync(RefreshTokenClaimsMode.Current);

        var client = factory.CreateClient();
        await client.LoginAliceAsync();
        var issued = await client.GetTokensAsync(clientId: clientId);
        Assert.False(ReadJwtPayload(issued.AccessToken!).TryGetProperty("website", out _));

        await using var claim = await AddUserClaimAsync("website", "https://alice.example");
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
        var clientId = await RegisterClientAsync("refresh_current_removed_claim_client");
        await using var mode = await UseClaimsModeAsync(RefreshTokenClaimsMode.Current);

        var client = factory.CreateClient();
        await client.LoginAliceAsync();

        TokenEndpointParameters issued;
        await using (await AddUserClaimAsync("website", "https://removed-before-refresh.example"))
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
        var clientId = await RegisterClientAsync("refresh_snapshot_mode_client");
        await using var mode = await UseClaimsModeAsync(RefreshTokenClaimsMode.Snapshot);

        var client = factory.CreateClient();
        await client.LoginAliceAsync();
        var issued = await client.GetTokensAsync(clientId: clientId);

        await using var claim = await AddUserClaimAsync("website", "https://added-after.example");
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
        var clientId = await RegisterClientAsync(
            "refresh_snapshot_distinct_claim_sets_client",
            configure: client =>
            {
                client.ClientClaimsPrefix = string.Empty;
                client.Claims.Add(new Claim("access_only", "client-value"));
            });
        await using var mode = await UseClaimsModeAsync(RefreshTokenClaimsMode.Snapshot);

        var client = factory.CreateClient();
        await client.LoginAliceAsync();

        TokenEndpointParameters issued;
        await using (await AddUserClaimAsync("website", "https://snapshot.example"))
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
        await using var currentMode = await UseClaimsModeAsync(
            RefreshTokenClaimsMode.Current,
            factory.Handles.Demo);
        await using var snapshotMode = await UseClaimsModeAsync(
            RefreshTokenClaimsMode.Snapshot,
            factory.Handles.Server);
        var currentClientId = await RegisterClientAsync(
            "refresh_two_realms_current",
            factory.Handles.Demo);
        var snapshotClientId = await RegisterClientAsync(
            "refresh_two_realms_snapshot",
            factory.Handles.Server);

        var currentClient = factory.CreateClient();
        await currentClient.LoginAliceAsync();
        var currentIssued = await currentClient.GetTokensAsync(clientId: currentClientId);

        var snapshotClient = factory.CreateClient();
        await snapshotClient.LoginAsync(
            factory.Handles.Alice.Username,
            factory.Handles.Alice.Password,
            factory.Handles.Server.Path);
        var snapshotIssued = await snapshotClient.GetTokensAsync(
            snapshotClientId, "openid profile offline_access", factory.Handles.Server.Path);

        await using var demoClaim = await AddUserClaimAsync(
            "website", "https://current-realm.example", factory.Handles.Demo);
        await using var serverClaim = await AddUserClaimAsync(
            "website", "https://snapshot-realm.example", factory.Handles.Server);

        var currentRefreshed = await RefreshAsync(
            currentClient, currentIssued.RefreshToken!, currentClientId, factory.Handles.Demo);
        var snapshotRefreshed = await RefreshAsync(
            snapshotClient, snapshotIssued.RefreshToken!, snapshotClientId, factory.Handles.Server);

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
        var clientId = await RegisterClientAsync(
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
        var clientId = await RegisterClientAsync(
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
        var clientId = await RegisterClientAsync(
            "refresh_expired_finite_tolerance_client",
            configure: client => client.RefreshTokenPostConsumedTimeTolerance = TimeSpan.FromMinutes(5));
        var client = factory.CreateClient();
        await client.LoginAliceAsync();
        var issued = await client.GetTokensAsync(clientId: clientId);
        await RefreshAsync(client, issued.RefreshToken!, clientId);

        var realm = await factory.LoadRealmAsync(factory.Handles.Demo);
        var consumed = await factory.WithStorageAsync(
            storage => storage.GetRefreshTokenStore(realm).GetAsync(issued.RefreshToken!, default));
        Assert.NotNull(consumed);
        Assert.NotNull(consumed!.ConsumedTime);
        await factory.SetRefreshTokenConsumedTimeAsync(
            factory.Handles.Demo,
            issued.RefreshToken!,
            consumed.ConsumedTime.Value.AddMinutes(-6));

        await AssertInvalidGrantAsync(await RefreshResponseAsync(client, issued.RefreshToken!, clientId));
    }

    [Fact]
    public async Task ConsumedToken_WithInfiniteTolerance_IsAccepted()
    {
        var clientId = await RegisterClientAsync(
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
        var clientId = await RegisterClientAsync(
            $"refresh_inactive_{claimsMode.ToString().ToLowerInvariant()}");
        await using var mode = await UseClaimsModeAsync(claimsMode);
        var client = factory.CreateClient();
        await client.LoginAliceAsync();
        var issued = await client.GetTokensAsync(clientId: clientId);

        await using var inactive = await SetAliceActiveAsync(false);
        await AssertInvalidGrantAsync(await RefreshResponseAsync(client, issued.RefreshToken!, clientId));
    }

    [Theory]
    [InlineData(RefreshTokenClaimsMode.Current)]
    [InlineData(RefreshTokenClaimsMode.Snapshot)]
    public async Task BothClaimsModes_RejectAnEndedSession(RefreshTokenClaimsMode claimsMode)
    {
        var clientId = await RegisterClientAsync(
            $"refresh_ended_session_{claimsMode.ToString().ToLowerInvariant()}");
        await using var mode = await UseClaimsModeAsync(claimsMode);
        var client = factory.CreateClient();
        await client.LoginAliceAsync();
        var issued = await client.GetTokensAsync(clientId: clientId);
        var realm = await factory.LoadRealmAsync(factory.Handles.Demo);
        var refreshToken = await factory.WithStorageAsync(
            storage => storage.GetRefreshTokenStore(realm).GetAsync(issued.RefreshToken!, default));
        Assert.NotNull(refreshToken);
        Assert.NotNull(refreshToken!.SessionId);
        await factory.WithStorageAsync(
            storage => storage.GetUserSessionStore(realm).EndAsync(refreshToken.SessionId!, default));

        await AssertInvalidGrantAsync(await RefreshResponseAsync(client, issued.RefreshToken!, clientId));
    }

    [Fact]
    public async Task InvalidTargetAfterTheCas_LeavesTheRefreshTokenConsumed()
    {
        var clientId = await RegisterClientAsync(
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
        var realm = await factory.LoadRealmAsync(factory.Handles.Demo);
        var original = await factory.WithStorageAsync(
            storage => storage.GetRefreshTokenStore(realm).GetAsync(issued.RefreshToken!, default));
        Assert.NotNull(original);
        var resourceServerName = $"refresh-unauthorized-{Guid.NewGuid():N}";
        var unauthorizedResource = $"https://unauthorized.example/{Guid.NewGuid():N}";
        factory.Resources.SetResourceServer(factory.Handles.Demo.Id, new ResourceServer(
            ScopeVisibility.Public,
            resourceServerName,
            "Unauthorized resource",
            "Known by the realm but outside the original grant")
        {
            ProtectedResources = [new ProtectedResource(unauthorizedResource)],
        });
        await using var resource = new AsyncRevert(() =>
        {
            factory.Resources.RemoveResourceServer(factory.Handles.Demo.Id, resourceServerName);
            return Task.CompletedTask;
        });

        var invalidTarget = await RefreshResponseAsync(
            client,
            issued.RefreshToken!,
            clientId,
            resource: unauthorizedResource);
        await AssertProtocolErrorAsync(invalidTarget, "invalid_target");

        var persisted = await factory.WithStorageAsync(
            storage => storage.GetRefreshTokenStore(realm).GetAsync(issued.RefreshToken!, default));
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
