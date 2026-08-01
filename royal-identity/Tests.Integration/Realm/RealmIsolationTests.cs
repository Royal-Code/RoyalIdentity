using Microsoft.Extensions.DependencyInjection;
using RoyalIdentity.Contracts;
using RoyalIdentity.Contracts.Storage;
using RoyalIdentity.Events;
using RoyalIdentity.Extensions;
using RoyalIdentity.Models;
using RoyalIdentity.Models.Tokens;
using RoyalIdentity.Utils;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Tests.Integration.Prepare;
using RoyalIdentity.Contracts.Defaults;
using RoyalIdentity.Options;

namespace Tests.Integration.Realm;

public class RealmIsolationTests : IClassFixture<PersistentStorageAppFactory>
{
    private readonly PersistentStorageAppFactory factory;

    public RealmIsolationTests(PersistentStorageAppFactory factory)
    {
        this.factory = factory;
    }

    private async Task<RoyalIdentity.Models.Realm> CreateRealmAsync(string suffix)
    {
        using var scope = factory.Services.CreateScope();
        var manager = scope.ServiceProvider.GetRequiredService<IRealmManager>();
        var path = $"realm-b-{suffix}";
        var realm = await manager.CreateAsync(path, $"{path}.test", $"Test Realm {suffix}");
        factory.Resources.SetResourceServer(
            realm.Id,
            TestConfigurationResourceSource.CreateDemoResourceServer());
        await factory.RefreshConfigurationAsync();
        return realm;
    }

    private Task SaveClientAsync(
        RoyalIdentity.Models.Realm realm,
        string clientId,
        string? secret = null,
        Action<TestClientBuilder>? configure = null)
        => factory.SaveClientAsync(new TestRealmHandle(realm.Id, realm.Path), clientId, client =>
        {
            client.Name = $"Realm isolation client {clientId}";
            client.RequireClientSecret = secret is not null;
            client.RequirePkce = false;
            client.AllowedGrantTypes.Add("client_credentials");
            client.AllowedScopes.Add("api");
            client.AllowedResourceServers.Add("apiserver");
            client.AllowedResponseTypes.Add("code");
            if (secret is not null)
                client.Secrets.Add(new ClientSecret(secret.Sha512()));
            configure?.Invoke(client);
        });

    private async Task<IAsyncDisposable> UseJwtPersistenceAsync()
    {
        var realm = await factory.LoadRealmAsync(factory.Handles.Demo);
        var previous = realm.Options.OperationalStorage.JwtAccessTokenPersistence;
        await factory.UpdateRealmAsync(
            factory.Handles.Demo,
            options => options.OperationalStorage.JwtAccessTokenPersistence =
                JwtAccessTokenPersistenceMode.Metadata);
        return new AsyncRevert(() => factory.UpdateRealmAsync(
            factory.Handles.Demo,
            options => options.OperationalStorage.JwtAccessTokenPersistence = previous));
    }

    private sealed class AsyncRevert(Func<Task> revert) : IAsyncDisposable
    {
        public ValueTask DisposeAsync() => new(revert());
    }

    // ─── Phase 2: RealmDiscoveryMiddleware ────────────────────────────────────

    [Fact]
    public async Task UnknownRealm_Returns404_WithJsonErrorBody()
    {
        var client = factory.CreateClient(new() { AllowAutoRedirect = false });
        var url = Oidc.Routes.BuildAuthorizeUrl("nonexistent-realm");

        var response = await client.GetAsync(url);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("realm_not_found", body.GetProperty("error").GetString());
        Assert.Contains("nonexistent-realm", body.GetProperty("error_description").GetString());
    }

    // ─── §8.1 Client Isolation ────────────────────────────────────────────────

    [Fact]
    public async Task ClientIsolation_ClientInRealmA_NotFoundInRealmB()
    {
        var realmB = await CreateRealmAsync(CryptoRandom.CreateUniqueId(6));
        using var storageScope = factory.CreateStorageScope();
        var storage = storageScope.Storage;

        var clientB = await storage.GetClientStore(realmB).FindClientByIdAsync("demo_client", default);

        Assert.Null(clientB);
    }

    [Fact]
    public async Task ClientIsolation_SameClientId_DifferentRealms_ReturnDifferentClients()
    {
        var realmB = await CreateRealmAsync(CryptoRandom.CreateUniqueId(6));
        await SaveClientAsync(realmB, "demo_client", configure: client =>
        {
            client.Name = "Demo Client in Realm B";
            client.RedirectUris.Add("http://realm-b.example.com/callback");
        });
        var demoRealm = await factory.LoadRealmAsync(factory.Handles.Demo);
        using var storageScope = factory.CreateStorageScope();
        var storage = storageScope.Storage;

        var clientA = await storage.GetClientStore(demoRealm).FindClientByIdAsync("demo_client", default);
        var clientB = await storage.GetClientStore(realmB).FindClientByIdAsync("demo_client", default);

        Assert.NotNull(clientA);
        Assert.NotNull(clientB);
        Assert.NotEqual(clientA.Realm.Id, clientB.Realm.Id);
        Assert.DoesNotContain(clientA.RedirectUris, u => u == "http://realm-b.example.com/callback");
        Assert.Contains(clientB.RedirectUris, u => u == "http://realm-b.example.com/callback");
    }

    // ─── §8.2 Session Isolation ───────────────────────────────────────────────

    [Fact]
    public async Task SessionIsolation_LoginInRealmA_DoesNotAuthenticateRealmB()
    {
        var realmB = await CreateRealmAsync(CryptoRandom.CreateUniqueId(6));
        var httpClient = factory.CreateClient(new() { AllowAutoRedirect = false });

        await httpClient.LoginAliceAsync();

        // Session cookie from realm A must not authenticate realm B —
        // ValidateUserSessionAsync checks the current request's realm session store,
        // where Alice has no session, so the challenge redirects to realm B login.
        var response = await httpClient.GetAsync($"/{realmB.Path}/test/account/profile");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains($"{realmB.Path}/account/login", response.Headers.Location?.ToString() ?? "");
    }

    // ─── §8.3 Token Store Isolation ───────────────────────────────────────────

    [Fact]
    public async Task TokenStoreIsolation_AccessTokenStoredOnlyInIssuingRealm()
    {
        var realmB = await CreateRealmAsync(CryptoRandom.CreateUniqueId(6));
        await using var persistence = await UseJwtPersistenceAsync();
        var clientId = $"cc-client-{CryptoRandom.CreateUniqueId(6)}";
        var secret = CryptoRandom.CreateUniqueId();

        var demoRealm = await factory.LoadRealmAsync(factory.Handles.Demo);
        await SaveClientAsync(demoRealm, clientId, secret);

        var client = factory.CreateClient();
        var url = Oidc.Routes.BuildTokenUrl(factory.Handles.Demo.Path);
        var response = await client.PostAsync(url, new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = clientId,
            ["client_secret"] = secret,
            ["scope"] = "api"
        }));

        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadFromJsonAsync<JsonElement>();
        var jwt = content.GetProperty("access_token").GetString()!;
        var jti = new JwtSecurityTokenHandler().ReadJwtToken(jwt).Id;

        using var storageScope = factory.CreateStorageScope();
        var storage = storageScope.Storage;
        var inRealmA = await storage.GetAccessTokenStore(demoRealm).GetAsync(jti, default);
        var inRealmB = await storage.GetAccessTokenStore(realmB).GetAsync(jti, default);

        Assert.NotNull(inRealmA);
        Assert.Null(inRealmB);
        Assert.Equal(factory.Handles.Demo.Id, inRealmA.RealmId);
    }

    [Fact]
    public async Task RefreshTokenIsolation_TokenFromRealmA_NotAcceptedInRealmB()
    {
        var realmB = await CreateRealmAsync(CryptoRandom.CreateUniqueId(6));

        var httpClient = factory.CreateClient();
        await httpClient.LoginAliceAsync();
        var tokens = await httpClient.GetTokensAsync("demo_client", "openid offline_access");

        var refreshToken = tokens.RefreshToken;
        Assert.NotNull(refreshToken);

        var url = Oidc.Routes.BuildTokenUrl(realmB.Path);
        var response = await httpClient.PostAsync(url, new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["client_id"] = "demo_client",
            ["refresh_token"] = refreshToken
        }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AuthCodeIsolation_Code_StoredOnlyInIssuingRealmStore()
    {
        var realmB = await CreateRealmAsync(CryptoRandom.CreateUniqueId(6));
        var demoRealm = await factory.LoadRealmAsync(factory.Handles.Demo);
        using var storageScope = factory.CreateStorageScope();
        var storage = storageScope.Storage;
        var resourcesStore = storage.GetResourceStore(demoRealm);
        var resources = await resourcesStore.FindResourcesByScopeAsync(["openid", "profile"], default);

        var code = new AuthorizationCode(
            "demo_client",
            SubjectFactory.Create("alice", "Alice", "admin"),
            DateTime.UtcNow,
            300,
            resources,
            "http://localhost:5000/callback")
        {
            RealmId = factory.Handles.Demo.Id
        };
        await storage.GetAuthorizationCodeStore(demoRealm).StoreAuthorizationCodeAsync(code, default);

        var inRealmA = await storage.GetAuthorizationCodeStore(demoRealm).GetAuthorizationCodeAsync(code.Code, default);
        var inRealmB = await storage.GetAuthorizationCodeStore(realmB).GetAuthorizationCodeAsync(code.Code, default);

        Assert.NotNull(inRealmA);
        Assert.Null(inRealmB);
    }

    [Fact]
    public async Task RevocationIsolation_AccessTokenFromRealmA_NotRevokedFromRealmB()
    {
        var realmB = await CreateRealmAsync(CryptoRandom.CreateUniqueId(6));
        await using var persistence = await UseJwtPersistenceAsync();
        var clientId = $"cc-revoke-{CryptoRandom.CreateUniqueId(6)}";
        var secret = CryptoRandom.CreateUniqueId();
        var demoRealm = await factory.LoadRealmAsync(factory.Handles.Demo);

        // Register same client in both realms — same client_id but realm B has no tokens
        await SaveClientAsync(demoRealm, clientId, secret);
        await SaveClientAsync(realmB, clientId, secret);

        var client = factory.CreateClient();
        var tokenUrl = Oidc.Routes.BuildTokenUrl(factory.Handles.Demo.Path);
        var tokenResponse = await client.PostAsync(tokenUrl, new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = clientId,
            ["client_secret"] = secret,
            ["scope"] = "api"
        }));
        tokenResponse.EnsureSuccessStatusCode();
        var tokenContent = await tokenResponse.Content.ReadFromJsonAsync<JsonElement>();
        var jwt = tokenContent.GetProperty("access_token").GetString()!;
        var jti = new JwtSecurityTokenHandler().ReadJwtToken(jwt).Id;

        // Attempt revocation from realm B — RFC 7009 returns 200 even for unknown token
        var revokeUrl = Oidc.Routes.BuildRevocationUrl(realmB.Path);
        var revokeResponse = await client.PostAsync(revokeUrl, new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["token"] = jwt,
            ["client_id"] = clientId,
            ["client_secret"] = secret
        }));
        Assert.Equal(HttpStatusCode.OK, revokeResponse.StatusCode);

        // Token must still exist in realm A store — realm B revocation cannot touch realm A's store
        var stillInRealmA = await factory.WithStorageAsync(
            storage => storage.GetAccessTokenStore(demoRealm).GetAsync(jti, default));
        Assert.NotNull(stillInRealmA);
    }

    [Fact]
    public async Task RefreshTokenRenewal_NewAccessToken_KeepsRealmId()
    {
        await using var persistence = await UseJwtPersistenceAsync();
        var httpClient = factory.CreateClient();
        var demoRealm = await factory.LoadRealmAsync(factory.Handles.Demo);

        await httpClient.LoginAliceAsync();
        var tokens = await httpClient.GetTokensAsync("demo_client", "openid offline_access");
        Assert.NotNull(tokens.RefreshToken);

        var tokenUrl = Oidc.Routes.BuildTokenUrl(factory.Handles.Demo.Path);
        var response = await httpClient.PostAsync(tokenUrl, new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["client_id"] = "demo_client",
            ["refresh_token"] = tokens.RefreshToken
        }));
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadFromJsonAsync<JsonElement>();
        var jwt = content.GetProperty("access_token").GetString()!;
        var jti = new JwtSecurityTokenHandler().ReadJwtToken(jwt).Id;

        var newToken = await factory.WithStorageAsync(
            storage => storage.GetAccessTokenStore(demoRealm).GetAsync(jti, default));
        Assert.NotNull(newToken);
        Assert.Equal(factory.Handles.Demo.Id, newToken.RealmId);
    }

    [Fact]
    public async Task RevocationIsolation_RefreshTokenFromRealmA_NotRevokedFromRealmB()
    {
        var realmB = await CreateRealmAsync(CryptoRandom.CreateUniqueId(6));
        await SaveClientAsync(realmB, "demo_client", configure: client =>
        {
            client.Name = "Demo Client (realm B)";
            client.RequireClientSecret = false;
            client.AllowedGrantTypes.Clear();
            client.AllowedGrantTypes.Add("authorization_code");
            client.AllowedIdentityScopes.Add("openid");
            client.AllowOfflineAccess = true;
            client.RedirectUris.Add("http://localhost:5000/callback");
        });
        var demoRealm = await factory.LoadRealmAsync(factory.Handles.Demo);

        var httpClient = factory.CreateClient();
        await httpClient.LoginAliceAsync();
        var tokens = await httpClient.GetTokensAsync("demo_client", "openid offline_access");
        Assert.NotNull(tokens.RefreshToken);

        var revokeUrl = Oidc.Routes.BuildRevocationUrl(realmB.Path);
        var revokeResponse = await httpClient.PostAsync(revokeUrl, new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["token"] = tokens.RefreshToken,
            ["client_id"] = "demo_client"
        }));
        Assert.Equal(HttpStatusCode.OK, revokeResponse.StatusCode);

        var stillInRealmA = await factory.WithStorageAsync(
            storage => storage.GetRefreshTokenStore(demoRealm).GetAsync(tokens.RefreshToken, default));
        Assert.NotNull(stillInRealmA);
    }

    [Fact]
    public async Task AuthCodeIsolation_CodeFromRealmA_NotRedeemableInRealmB()
    {
        var realmB = await CreateRealmAsync(CryptoRandom.CreateUniqueId(6));
        await SaveClientAsync(realmB, "demo_client", configure: client =>
        {
            client.Name = "Demo Client (realm B)";
            client.RequireClientSecret = false;
            client.AllowedGrantTypes.Clear();
            client.AllowedGrantTypes.Add("authorization_code");
            client.AllowedIdentityScopes.UnionWith(["openid", "profile"]);
            client.RedirectUris.Add("http://localhost:5000/callback");
        });

        var demoRealm = await factory.LoadRealmAsync(factory.Handles.Demo);
        using var storageScope = factory.CreateStorageScope();
        var storage = storageScope.Storage;
        var resourcesStore = storage.GetResourceStore(demoRealm);
        var resources = await resourcesStore.FindResourcesByScopeAsync(["openid", "profile"], default);

        var code = new AuthorizationCode(
            "demo_client",
            SubjectFactory.Create("alice", "Alice", "admin"),
            DateTime.UtcNow,
            300,
            resources,
            "http://localhost:5000/callback")
        {
            RealmId = factory.Handles.Demo.Id
        };
        await storage.GetAuthorizationCodeStore(demoRealm).StoreAuthorizationCodeAsync(code, default);

        var httpClient = factory.CreateClient();
        var tokenUrl = Oidc.Routes.BuildTokenUrl(realmB.Path);
        var response = await httpClient.PostAsync(tokenUrl, new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code.Code,
            ["client_id"] = "demo_client",
            ["redirect_uri"] = "http://localhost:5000/callback"
        }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AccessTokenStoreIsolation_JtiFromRealmA_NotFoundInRealmB()
    {
        var realmB = await CreateRealmAsync(CryptoRandom.CreateUniqueId(6));
        var clientId = $"ref-token-{CryptoRandom.CreateUniqueId(6)}";
        var secret = CryptoRandom.CreateUniqueId();
        var demoRealm = await factory.LoadRealmAsync(factory.Handles.Demo);

        await SaveClientAsync(demoRealm, clientId, secret);

        var httpClient = factory.CreateClient();
        var url = Oidc.Routes.BuildTokenUrl(factory.Handles.Demo.Path);
        var tokenResponse = await httpClient.PostAsync(url, new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = clientId,
            ["client_secret"] = secret,
            ["scope"] = "api"
        }));
        tokenResponse.EnsureSuccessStatusCode();

        var content = await tokenResponse.Content.ReadFromJsonAsync<JsonElement>();
        var jwt = content.GetProperty("access_token").GetString()!;
        var jti = new JwtSecurityTokenHandler().ReadJwtToken(jwt).Id;

        using var scope = factory.Services.CreateScope();
        var validator = scope.ServiceProvider.GetRequiredService<ITokenValidator>();

        var result = await validator.ValidateReferenceAccessTokenAsync(realmB, jti, default);

        Assert.True(result.HasError, "Token from realm A must not validate in realm B");
    }

    // ─── §8.4 Consent Isolation ───────────────────────────────────────────────

    [Fact]
    public async Task ConsentIsolation_ConsentInRealmA_NotVisibleInRealmB()
    {
        var realmB = await CreateRealmAsync(CryptoRandom.CreateUniqueId(6));
        var demoRealm = await factory.LoadRealmAsync(factory.Handles.Demo);
        using var storageScope = factory.CreateStorageScope();
        var storage = storageScope.Storage;

        var consent = new Consent
        {
            SubjectId = "alice",
            ClientId = "consent-app",
            RealmId = factory.Handles.Demo.Id,
            CreationTime = DateTime.UtcNow
        };
        await storage.GetUserConsentStore(demoRealm).StoreUserConsentAsync(consent, default);

        var inRealmB = await storage.GetUserConsentStore(realmB).GetUserConsentAsync("alice", "consent-app", default);

        Assert.Null(inRealmB);
    }

    [Fact]
    public async Task DefaultConsentService_StoresConsent_WithRealmId()
    {
        using var scope = factory.Services.CreateScope();
        var storage = scope.ServiceProvider.GetRequiredService<IStorage>();
        var consentService = scope.ServiceProvider.GetRequiredService<IConsentService>();
        var demoRealm = await storage.Realms.GetByIdAsync(factory.Handles.Demo.Id, default);
        Assert.NotNull(demoRealm);

        var client = await storage.GetClientStore(demoRealm).FindClientByIdAsync("demo_consent_client", default);
        Assert.NotNull(client);

        var subject = SubjectFactory.Create("alice-consent-realm-test", "Alice", "user");
        var scopes = new[] { new ConsentedScope { Scope = "openid" }, new ConsentedScope { Scope = "profile" } };

        await consentService.UpdateConsentAsync(subject, client, scopes, default);

        var consent = await storage.GetUserConsentStore(demoRealm)
            .GetUserConsentAsync("alice-consent-realm-test", "demo_consent_client", default);
        Assert.NotNull(consent);
        Assert.Equal(factory.Handles.Demo.Id, consent.RealmId);
    }

    // ─── §8.5 RealmId in Tokens ───────────────────────────────────────────────

    [Fact]
    public async Task TokenCreation_AccessToken_HasCorrectRealmId()
    {
        await using var persistence = await UseJwtPersistenceAsync();
        var clientId = $"realm-id-check-{CryptoRandom.CreateUniqueId(6)}";
        var secret = CryptoRandom.CreateUniqueId();
        var demoRealm = await factory.LoadRealmAsync(factory.Handles.Demo);

        await SaveClientAsync(demoRealm, clientId, secret);

        var client = factory.CreateClient();
        var url = Oidc.Routes.BuildTokenUrl(factory.Handles.Demo.Path);
        var response = await client.PostAsync(url, new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = clientId,
            ["client_secret"] = secret,
            ["scope"] = "api"
        }));

        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadFromJsonAsync<JsonElement>();
        var jwt = content.GetProperty("access_token").GetString()!;
        var jti = new JwtSecurityTokenHandler().ReadJwtToken(jwt).Id;

        var token = await factory.WithStorageAsync(
            storage => storage.GetAccessTokenStore(demoRealm).GetAsync(jti, default));

        Assert.NotNull(token);
        Assert.Equal(factory.Handles.Demo.Id, token.RealmId);
    }

    [Fact]
    public async Task DefaultCodeFactory_CreatesCode_WithCorrectRealmId()
    {
        var httpClient = factory.CreateClient(new() { AllowAutoRedirect = false });
        var demoRealm = await factory.LoadRealmAsync(factory.Handles.Demo);

        await httpClient.LoginAliceAsync();
        var codeValue = await httpClient.GetAuthorizeAsync();
        Assert.NotNull(codeValue);

        var code = await factory.WithStorageAsync(
            storage => storage.GetAuthorizationCodeStore(demoRealm)
                .GetAuthorizationCodeAsync(codeValue, default));
        Assert.NotNull(code);
        Assert.Equal(factory.Handles.Demo.Id, code.RealmId);
    }

    // ─── §8.6 IRealmManager ───────────────────────────────────────────────────

    [Fact]
    public async Task RealmManager_CreateAsync_CreatesRealmAndInitializesStores()
    {
        var suffix = CryptoRandom.CreateUniqueId(6);

        var realm = await CreateRealmAsync(suffix);
        using var storageScope = factory.CreateStorageScope();
        var storage = storageScope.Storage;

        Assert.NotNull(realm);
        Assert.Equal($"realm-b-{suffix}", realm.Path);

        var foundByPath = await storage.Realms.GetByPathAsync(realm.Path, default);
        Assert.NotNull(foundByPath);

        // stores must be functional — no exception
        var clientStore = storage.GetClientStore(realm);
        var tokenStore = storage.GetAccessTokenStore(realm);
        Assert.NotNull(clientStore);
        Assert.NotNull(tokenStore);
    }

    [Fact]
    public async Task RealmManager_CreateAsync_NormalizesDomainToLowercase()
    {
        using var scope = factory.Services.CreateScope();
        var manager = scope.ServiceProvider.GetRequiredService<IRealmManager>();
        var suffix = CryptoRandom.CreateUniqueId(6).ToLowerInvariant();

        var realm = await manager.CreateAsync(
            $"normalized-{suffix}",
            $"Mixed-{suffix}.Contract.Test",
            "Normalized domain");

        Assert.Equal($"mixed-{suffix}.contract.test", realm.Domain);
        var stored = await factory.WithStorageValueAsync(
            storage => storage.Realms.GetByDomainAsync(realm.Domain));
        Assert.NotNull(stored);
        Assert.Equal(realm.Id, stored.Id);
    }

    [Fact]
    public async Task RealmManager_CreateAsync_DuplicatePath_Throws()
    {
        using var scope = factory.Services.CreateScope();
        var manager = scope.ServiceProvider.GetRequiredService<IRealmManager>();
        var suffix = CryptoRandom.CreateUniqueId(6);
        var path = $"dup-realm-{suffix}";

        await manager.CreateAsync(path, $"{path}.test", "First");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => manager.CreateAsync(path, $"{path}2.test", "Second").AsTask());
    }

    [Fact]
    public async Task RealmManager_DisableAsync_InternalRealm_IsRejected()
    {
        using var scope = factory.Services.CreateScope();
        var manager = scope.ServiceProvider.GetRequiredService<IRealmManager>();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => manager.DisableAsync(factory.Handles.Server.Id, default).AsTask());
    }

    [Fact]
    public async Task RealmManager_DeleteAsync_InternalRealm_ReturnsFalse()
    {
        using var storageScope = factory.CreateStorageScope();
        var storage = storageScope.Storage;

        var deleted = await storage.Realms.DeleteAsync("server", default);

        Assert.False(deleted);
        var serverRealm = await storage.Realms.GetByIdAsync("server", default);
        Assert.NotNull(serverRealm);
    }

    [Fact]
    public async Task RealmStore_DeleteAsync_TombstonesRealm()
    {
        var suffix = CryptoRandom.CreateUniqueId(6);
        var realm = await CreateRealmAsync(suffix);
        using var storageScope = factory.CreateStorageScope();
        var storage = storageScope.Storage;

        var deleted = await storage.Realms.DeleteAsync(realm.Id, default);

        Assert.True(deleted);

        var notFound = await storage.Realms.GetByIdAsync(realm.Id, default);
        Assert.Null(notFound);

    }

    [Fact]
    public async Task RealmManager_EnableDisable_RoundTrip()
    {
        using var scope = factory.Services.CreateScope();
        var manager = scope.ServiceProvider.GetRequiredService<IRealmManager>();
        var storage = scope.ServiceProvider.GetRequiredService<IStorage>();
        var suffix = CryptoRandom.CreateUniqueId(6);

        var realm = await manager.CreateAsync($"toggle-{suffix}", $"toggle-{suffix}.test", "Toggle Realm");
        Assert.True(realm.Enabled);

        await manager.DisableAsync(realm.Id);
        var afterDisable = await storage.Realms.GetByIdAsync(realm.Id, default);
        Assert.False(afterDisable!.Enabled);

        await manager.EnableAsync(realm.Id);
        var afterEnable = await storage.Realms.GetByIdAsync(realm.Id, default);
        Assert.True(afterEnable!.Enabled);
    }

    [Fact]
    public async Task RealmManager_UpdateAsync_UpdatesRealmOptions()
    {
        using var scope = factory.Services.CreateScope();
        var manager = scope.ServiceProvider.GetRequiredService<IRealmManager>();
        var storage = scope.ServiceProvider.GetRequiredService<IStorage>();
        var suffix = CryptoRandom.CreateUniqueId(6);

        var realm = await manager.CreateAsync($"upd-{suffix}", $"upd-{suffix}.test", "Update Test Realm");
        realm.Options.Branding.PrimaryColor = "#ABCDEF";

        await manager.UpdateAsync(realm);

        var updated = await storage.Realms.GetByIdAsync(realm.Id, default);
        Assert.NotNull(updated);
        Assert.Equal("#ABCDEF", updated.Options.Branding.PrimaryColor);
    }

    [Fact]
    public async Task RealmManager_UpdateAsync_DifferentPath_Throws()
    {
        using var scope = factory.Services.CreateScope();
        var manager = scope.ServiceProvider.GetRequiredService<IRealmManager>();
        var suffix = CryptoRandom.CreateUniqueId(6);

        var realm = await manager.CreateAsync($"orig-{suffix}", $"orig-{suffix}.test", "Original Realm");

        // Construct a new Realm with the same ID but a different path
        var spoofed = new RoyalIdentity.Models.Realm(realm.Id, realm.Domain, "spoofed-path", realm.DisplayName, false, realm.Options);

        await Assert.ThrowsAsync<InvalidOperationException>(() => manager.UpdateAsync(spoofed).AsTask());
    }

    [Fact]
    public async Task RealmManager_UpdateAsync_DifferentDomain_Throws()
    {
        using var scope = factory.Services.CreateScope();
        var manager = scope.ServiceProvider.GetRequiredService<IRealmManager>();
        var suffix = CryptoRandom.CreateUniqueId(6);

        var realm = await manager.CreateAsync($"ddom-{suffix}", $"ddom-{suffix}.test", "Domain Test Realm");

        var spoofed = new RoyalIdentity.Models.Realm(realm.Id, "spoofed.example.com", realm.Path, realm.DisplayName, false, realm.Options);

        await Assert.ThrowsAsync<InvalidOperationException>(() => manager.UpdateAsync(spoofed).AsTask());
    }

    // ─── §8.7 Events Realm-Scoped ─────────────────────────────────────────────

    [Fact]
    public async Task EventDispatcher_DispatchAsyncWithRealm_SetsRealmId()
    {
        using var scope = factory.Services.CreateScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IEventDispatcher>();
        var evt = new TestEvent();
        var demoRealm = await factory.LoadRealmAsync(factory.Handles.Demo);

        await dispatcher.DispatchAsync(evt, demoRealm);

        Assert.Equal(factory.Handles.Demo.Id, evt.RealmId);
    }

    [Fact]
    public async Task EventDispatcher_DispatchAsyncWithoutRealm_KeepsRealmIdNull()
    {
        using var scope = factory.Services.CreateScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IEventDispatcher>();
        var evt = new TestEvent();

        await dispatcher.DispatchAsync(evt);

        Assert.Null(evt.RealmId);
    }

    // ─── §8.8 Branding ────────────────────────────────────────────────────────

    [Fact]
    public async Task AccountLayout_WithRealmBranding_RendersPrimaryColorStyle()
    {
        using var scope = factory.Services.CreateScope();
        var manager = scope.ServiceProvider.GetRequiredService<IRealmManager>();
        var suffix = CryptoRandom.CreateUniqueId(6);
        var realm = await manager.CreateAsync($"bpc-{suffix}", $"bpc-{suffix}.test", "Brand Color Test");

        realm.Options.Branding.PrimaryColor = "#6366F1";
        await manager.UpdateAsync(realm);

        var client = factory.CreateClient();
        var response = await client.GetAsync($"/{realm.Path}/account/login");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("--primary-color: #6366F1", html);
    }

    [Fact]
    public async Task AccountLayout_WithoutBranding_UsesDefaultLogo()
    {
        using var scope = factory.Services.CreateScope();
        var manager = scope.ServiceProvider.GetRequiredService<IRealmManager>();
        var suffix = CryptoRandom.CreateUniqueId(6);
        var realm = await manager.CreateAsync($"bnb-{suffix}", $"bnb-{suffix}.test", "No Brand Test");

        realm.Options.Branding.LogoUri = null;
        realm.Options.Branding.FaviconUri = null;
        realm.Options.Branding.PrimaryColor = null;
        await manager.UpdateAsync(realm);

        var client = factory.CreateClient();
        var response = await client.GetAsync($"/{realm.Path}/account/login");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("icon.png", html);
    }

    [Fact]
    public async Task AccountLayout_WithRealmBranding_RendersRealmLogo()
    {
        using var scope = factory.Services.CreateScope();
        var manager = scope.ServiceProvider.GetRequiredService<IRealmManager>();
        var suffix = CryptoRandom.CreateUniqueId(6);
        var realm = await manager.CreateAsync($"blo-{suffix}", $"blo-{suffix}.test", "Logo Test");

        realm.Options.Branding.LogoUri = "/logo-test.png";
        await manager.UpdateAsync(realm);

        var client = factory.CreateClient();
        var response = await client.GetAsync($"/{realm.Path}/account/login");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("src=\"/logo-test.png\"", html);
    }

    [Fact]
    public async Task AccountLayout_WithRealmBranding_InjectsFaviconAndPrimaryColor()
    {
        using var scope = factory.Services.CreateScope();
        var manager = scope.ServiceProvider.GetRequiredService<IRealmManager>();
        var suffix = CryptoRandom.CreateUniqueId(6);
        var realm = await manager.CreateAsync($"bfi-{suffix}", $"bfi-{suffix}.test", "Favicon Test");

        realm.Options.Branding.FaviconUri = "/test-fav.ico";
        realm.Options.Branding.PrimaryColor = "#FF5733";
        await manager.UpdateAsync(realm);

        var client = factory.CreateClient();
        var response = await client.GetAsync($"/{realm.Path}/account/login");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("href=\"/test-fav.ico\"", html);
        Assert.Contains("--primary-color: #FF5733", html);
    }

    [Fact]
    public async Task AccountLayout_WithoutRealm_DoesNotThrow()
    {
        var client = factory.CreateClient();
        var response = await client.GetAsync("/nonexistent-realm-branding/account/login");

        Assert.Equal(System.Net.HttpStatusCode.NotFound, response.StatusCode);
    }

    // Helper event type for dispatcher tests
    private class TestEvent() : Event("Test", "Test Event", EventTypes.Information);
}
