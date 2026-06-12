// Ignore Spelling: Pkce

using Microsoft.Extensions.DependencyInjection;
using RoyalIdentity.Models;
using RoyalIdentity.Models.Scopes;
using RoyalIdentity.Contracts.Storage;
using RoyalIdentity.Extensions;
using RoyalIdentity.Utils;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Tests.Integration.Prepare;

namespace Tests.Integration.Endpoints;

public class CodeTokenTests : IClassFixture<AppFactory>
{
    private static readonly string[] scopeNames = ["openid", "profile"];

    private readonly AppFactory factory;

    public CodeTokenTests(AppFactory factory)
    {
        this.factory = factory;
    }

    [Fact]
    public async Task Post_WhenValidCode_Must_GenerateToken()
    {
        // Arrange
        var storage = factory.Services.GetRequiredService<IStorage>();
        var codeStore = storage.GetAuthorizationCodeStore(MemoryStorage.DemoRealm);
        var resourcesStore = storage.GetResourceStore(MemoryStorage.DemoRealm);

        var resources = await resourcesStore.FindResourcesByScopeAsync(scopeNames, default);
        var code = new RoyalIdentity.Models.Tokens.AuthorizationCode(
            "demo_client",
            SubjectFactory.Create("alice", "Test Name", "admin"),
            "session",
            DateTime.UtcNow,
            300,
            resources,
            "http://localhost:5000/callback");

        await codeStore.StoreAuthorizationCodeAsync(code, default);

        var client = factory.CreateClient();
        var url = Oidc.Routes.BuildTokenUrl(MemoryStorage.DemoRealm.Path);

        // Act
        var response = await client.PostAsync(url, 
            new FormUrlEncodedContent(
                new Dictionary<string, string>
                {
                    ["grant_type"] = "authorization_code",
                    ["code"] = code.Code,
                    ["client_id"] = "demo_client",
                    ["redirect_uri"] = "http://localhost:5000/callback"
                }));

        var content = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();

        // Assert
        Assert.True(
            response.StatusCode == HttpStatusCode.OK,
            await response.Content.ReadAsStringAsync());

        Assert.NotNull(content);
        Assert.True(content.ContainsKey("access_token"));
        Assert.True(content.ContainsKey("token_type"));
        Assert.True(content.ContainsKey("expires_in"));
        Assert.True(content.ContainsKey("scope"));
        Assert.True(content.ContainsKey("id_token"));
    }

    [Fact]
    public async Task Post_WhenValidCode_WithPkce_Must_GenerateToken()
    {
        // Arrange
        var storage = factory.Services.GetRequiredService<IStorage>();
        var codeStore = storage.GetAuthorizationCodeStore(MemoryStorage.DemoRealm);
        var resourcesStore = storage.GetResourceStore(MemoryStorage.DemoRealm);

        var resources = await resourcesStore.FindResourcesByScopeAsync(scopeNames, default);
        var codeVerifier = CryptoRandom.CreateUniqueId();
        var codeChallenge = PkceHelper.GenerateCodeChallengeS256(codeVerifier);

        var code = new RoyalIdentity.Models.Tokens.AuthorizationCode(
            "demo_client",
            SubjectFactory.Create("alice", "Test Name", "admin"),
            "session",
            DateTime.UtcNow,
            300,
            resources,
            "http://localhost:5000/callback")
        {
            CodeChallenge = codeChallenge,
            CodeChallengeMethod = "S256"
        };

        await codeStore.StoreAuthorizationCodeAsync(code, default);

        var client = factory.CreateClient();
        var url = Oidc.Routes.BuildTokenUrl(MemoryStorage.DemoRealm.Path);

        // Act
        var response = await client.PostAsync(url,
            new FormUrlEncodedContent(
                new Dictionary<string, string>
                {
                    ["grant_type"] = "authorization_code",
                    ["code"] = code.Code,
                    ["client_id"] = "demo_client",
                    ["redirect_uri"] = "http://localhost:5000/callback",
                    ["code_verifier"] = codeVerifier
                }));

        // Assert
        var content = await response.Content.ReadAsStringAsync();
        var token = JsonSerializer.Deserialize<Dictionary<string, object>>(content);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        Assert.NotNull(token);
        Assert.True(token.ContainsKey("access_token"));
        Assert.True(token.ContainsKey("token_type"));
        Assert.True(token.ContainsKey("expires_in"));
        Assert.True(token.ContainsKey("scope"));
        Assert.True(token.ContainsKey("id_token"));
    }

    [Fact]
    public async Task Post_WhenNotValidCode_WithPkce_Must_BadRequest()
    {
        // Arrange
        var storage = factory.Services.GetRequiredService<IStorage>();
        var codeStore = storage.GetAuthorizationCodeStore(MemoryStorage.DemoRealm);
        var resourcesStore = storage.GetResourceStore(MemoryStorage.DemoRealm);

        var resources = await resourcesStore.FindResourcesByScopeAsync(scopeNames, default);
        var codeVerifier = CryptoRandom.CreateUniqueId();
        var codeChallenge = PkceHelper.GenerateCodeChallengeS256(codeVerifier);

        var code = new RoyalIdentity.Models.Tokens.AuthorizationCode(
            "demo_client",
            SubjectFactory.Create("alice", "Test Name", "admin"),
            "session",
            DateTime.UtcNow,
            300,
            resources,
            "http://localhost:5000/callback")
        {
            CodeChallenge = codeChallenge,
            CodeChallengeMethod = "S256"
        };

        await codeStore.StoreAuthorizationCodeAsync(code, default);

        var client = factory.CreateClient();
        var url = Oidc.Routes.BuildTokenUrl(MemoryStorage.DemoRealm.Path);

        // Act
        var response = await client.PostAsync(url,
            new FormUrlEncodedContent(
                new Dictionary<string, string>
                {
                    ["grant_type"] = "authorization_code",
                    ["code"] = CryptoRandom.CreateUniqueId(),
                    ["client_id"] = "demo_client",
                    ["redirect_uri"] = "http://localhost:5000/callback",
                    ["code_verifier"] = codeVerifier
                }));

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Post_WhenNotValidCode_Must_BadRequest()
    {
        // Arrange
        var storage = factory.Services.GetRequiredService<IStorage>();
        var codeStore = storage.GetAuthorizationCodeStore(MemoryStorage.DemoRealm);
        var resourcesStore = storage.GetResourceStore(MemoryStorage.DemoRealm);

        var resources = await resourcesStore.FindResourcesByScopeAsync(scopeNames, default);

        var code = new RoyalIdentity.Models.Tokens.AuthorizationCode(
            "demo_client",
            SubjectFactory.Create("alice", "Test Name", "admin"),
            "session",
            DateTime.UtcNow,
            300,
            resources,
            "http://localhost:5000/callback");

        await codeStore.StoreAuthorizationCodeAsync(code, default);

        var client = factory.CreateClient();
        var url = Oidc.Routes.BuildTokenUrl(MemoryStorage.DemoRealm.Path);

        // Act
        var response = await client.PostAsync(url,
            new FormUrlEncodedContent(
                new Dictionary<string, string>
                {
                    ["grant_type"] = "authorization_code",
                    ["code"] = CryptoRandom.CreateUniqueId(),
                    ["client_id"] = "demo_client",
                    ["redirect_uri"] = "http://localhost:5000/callback",
                }));

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Post_WhenValidCode_WithClientSecret_Must_GenerateToken()
    {
        // Arrange
        var memoryStorage = factory.Services.GetRequiredService<MemoryStorage>();
        var storage = factory.Services.GetRequiredService<IStorage>();
        var codeStore = storage.GetAuthorizationCodeStore(MemoryStorage.DemoRealm);
        var resourcesStore = storage.GetResourceStore(MemoryStorage.DemoRealm);

        var clientId = "code_grant_type_client_1";
        var clientSecret = CryptoRandom.CreateUniqueId();
        var secretHash = clientSecret.Sha512();
        memoryStorage.GetDemoRealmStore().Clients.TryAdd(clientId, new RoyalIdentity.Models.Client()
        {
            Realm = MemoryStorage.DemoRealm,
            Id = clientId,
            Name = "Client with Secret",
            RequireClientSecret = true,
            RequirePkce = false,
            AllowOfflineAccess = true,
            AllowedIdentityScopes = { "openid", "profile", "email" },
            AllowedResponseTypes = { "code" },
            RedirectUris = { "http://localhost:5000/**", "https://localhost:5001/**" },
            ClientSecrets = { new RoyalIdentity.Models.ClientSecret(secretHash) }
        });

        var resources = await resourcesStore.FindResourcesByScopeAsync(scopeNames, default);
        var code = new RoyalIdentity.Models.Tokens.AuthorizationCode(
            clientId,
            SubjectFactory.Create("alice", "Test Name", "admin"),
            "session",
            DateTime.UtcNow,
            300,
            resources,
            "http://localhost:5000/callback");

        await codeStore.StoreAuthorizationCodeAsync(code, default);

        var client = factory.CreateClient();
        var url = Oidc.Routes.BuildTokenUrl(MemoryStorage.DemoRealm.Path);

        // Act
        var response = await client.PostAsync(url,
            new FormUrlEncodedContent(
                new Dictionary<string, string>
                {
                    ["grant_type"] = "authorization_code",
                    ["code"] = code.Code,
                    ["client_id"] = clientId,
                    ["client_secret"] = clientSecret,
                    ["redirect_uri"] = "http://localhost:5000/callback"
                }));

        // Assert
        var content = await response.Content.ReadAsStringAsync();
        var token = JsonSerializer.Deserialize<Dictionary<string, object>>(content);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        Assert.NotNull(token);
        Assert.True(token.ContainsKey("access_token"));
        Assert.True(token.ContainsKey("token_type"));
        Assert.True(token.ContainsKey("expires_in"));
        Assert.True(token.ContainsKey("scope"));
        Assert.True(token.ContainsKey("id_token"));
    }

    [Fact]
    public async Task Post_WhenValidCode_AndNoSecret_WithClientSecret_Must_BadRequest()
    {
        // Arrange
        var memoryStorage = factory.Services.GetRequiredService<MemoryStorage>();
        var storage = factory.Services.GetRequiredService<IStorage>();
        var codeStore = storage.GetAuthorizationCodeStore(MemoryStorage.DemoRealm);
        var resourcesStore = storage.GetResourceStore(MemoryStorage.DemoRealm);

        var clientId = "code_grant_type_client_2";
        var clientSecret = CryptoRandom.CreateUniqueId();
        var secretHash = clientSecret.Sha512();
        memoryStorage.GetDemoRealmStore().Clients.TryAdd(clientId, new RoyalIdentity.Models.Client()
        {
            Realm = MemoryStorage.DemoRealm,
            Id = clientId,
            Name = "Client with Secret",
            RequireClientSecret = true,
            RequirePkce = false,
            AllowOfflineAccess = true,
            AllowedIdentityScopes = { "openid", "profile", "email" },
            AllowedResponseTypes = { "code" },
            RedirectUris = { "http://localhost:5000/**", "https://localhost:5001/**" },
            ClientSecrets = { new RoyalIdentity.Models.ClientSecret(secretHash) }
        });

        var resources = await resourcesStore.FindResourcesByScopeAsync(scopeNames, default);
        var code = new RoyalIdentity.Models.Tokens.AuthorizationCode(
            clientId,
            SubjectFactory.Create("alice", "Test Name", "admin"),
            "session",
            DateTime.UtcNow,
            300,
            resources,
            "http://localhost:5000/callback");

        await codeStore.StoreAuthorizationCodeAsync(code, default);

        var client = factory.CreateClient();
        var url = Oidc.Routes.BuildTokenUrl(MemoryStorage.DemoRealm.Path);

        // Act
        var response = await client.PostAsync(url,
            new FormUrlEncodedContent(
                new Dictionary<string, string>
                {
                    ["grant_type"] = "authorization_code",
                    ["code"] = code.Code,
                    ["client_id"] = clientId,
                    ["redirect_uri"] = "http://localhost:5000/callback"
                }));

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Post_WhenCodeHasResourceIndicator_ShouldSetAudienceToResourceUri()
    {
        var storage = factory.Services.GetRequiredService<IStorage>();
        var memoryStorage = factory.Services.GetRequiredService<MemoryStorage>();
        var clientId = $"code-resource-client-{CryptoRandom.CreateUniqueId(4, CryptoRandom.OutputFormat.Hex)}";
        memoryStorage.GetDemoRealmStore().Clients[clientId] = new Client
        {
            Realm = MemoryStorage.DemoRealm,
            Id = clientId,
            Name = "Code Resource Client",
            RequireClientSecret = false,
            RequirePkce = false,
            AllowedGrantTypes = ["authorization_code"],
            AllowedIdentityScopes = { "openid" },
            AllowedResourceServers = { "apiserver" },
            AllowedResponseTypes = { "code" },
            RedirectUris = { "http://localhost:5000/**" }
        };

        var resourcesStore = storage.GetResourceStore(MemoryStorage.DemoRealm);
        var resources = await resourcesStore.FindRequestedResourcesAsync(
            ["openid"],
            ["https://api.demo.local/apiserver"],
            onlyEnabled: true);

        var code = new RoyalIdentity.Models.Tokens.AuthorizationCode(
            clientId,
            SubjectFactory.Create("alice", "Test Name", "admin"),
            "session",
            DateTime.UtcNow,
            300,
            resources,
            "http://localhost:5000/callback");

        await storage.GetAuthorizationCodeStore(MemoryStorage.DemoRealm).StoreAuthorizationCodeAsync(code, default);

        var client = factory.CreateClient();
        var url = Oidc.Routes.BuildTokenUrl(MemoryStorage.DemoRealm.Path);

        var response = await client.PostAsync(url,
            new FormUrlEncodedContent(
                new Dictionary<string, string>
                {
                    ["grant_type"] = "authorization_code",
                    ["code"] = code.Code,
                    ["client_id"] = clientId,
                    ["redirect_uri"] = "http://localhost:5000/callback"
                }));

        var content = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(content);
        var accessToken = content!["access_token"].ToString()!;
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(accessToken);
        Assert.Contains("https://api.demo.local/apiserver", jwt.Audiences);
        Assert.DoesNotContain("apiserver", jwt.Audiences);
    }

    [Fact]
    public async Task Post_WhenCodeTokenRequestUsesResourceSubset_ShouldSetSubsetAudience()
    {
        var storage = factory.Services.GetRequiredService<IStorage>();
        var memoryStorage = factory.Services.GetRequiredService<MemoryStorage>();
        var store = memoryStorage.GetDemoRealmStore();
        var suffix = CryptoRandom.CreateUniqueId(4, CryptoRandom.OutputFormat.Hex);
        var ordersServer = $"orders-{suffix}";
        var ordersResource = $"https://orders.demo.local/{suffix}";
        store.ResourceServers[ordersServer] = new ResourceServer(
            ScopeVisibility.Public,
            ordersServer,
            "Orders API",
            "Orders API")
        {
            ProtectedResources =
            [
                new ProtectedResource(ordersResource)
            ]
        };

        var clientId = $"code-subset-client-{suffix}";
        store.Clients[clientId] = new Client
        {
            Realm = MemoryStorage.DemoRealm,
            Id = clientId,
            Name = "Code Subset Client",
            RequireClientSecret = false,
            RequirePkce = false,
            AllowedGrantTypes = ["authorization_code"],
            AllowedIdentityScopes = { "openid" },
            AllowedResourceServers = { "apiserver", ordersServer },
            AllowedResponseTypes = { "code" },
            RedirectUris = { "http://localhost:5000/**" }
        };

        var resources = await storage.GetResourceStore(MemoryStorage.DemoRealm).FindRequestedResourcesAsync(
            ["openid"],
            ["https://api.demo.local/apiserver", ordersResource],
            onlyEnabled: true);

        var code = new RoyalIdentity.Models.Tokens.AuthorizationCode(
            clientId,
            SubjectFactory.Create("alice", "Test Name", "admin"),
            "session",
            DateTime.UtcNow,
            300,
            resources,
            "http://localhost:5000/callback");

        await storage.GetAuthorizationCodeStore(MemoryStorage.DemoRealm).StoreAuthorizationCodeAsync(code, default);

        var client = factory.CreateClient();
        var url = Oidc.Routes.BuildTokenUrl(MemoryStorage.DemoRealm.Path);

        var response = await client.PostAsync(url,
            new FormUrlEncodedContent(
                new Dictionary<string, string>
                {
                    ["grant_type"] = "authorization_code",
                    ["code"] = code.Code,
                    ["client_id"] = clientId,
                    ["redirect_uri"] = "http://localhost:5000/callback",
                    ["resource"] = ordersResource
                }));

        var content = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(content);
        var accessToken = content!["access_token"].ToString()!;
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(accessToken);
        Assert.Contains(ordersResource, jwt.Audiences);
        Assert.DoesNotContain("https://api.demo.local/apiserver", jwt.Audiences);
    }

    [Fact]
    public async Task Post_WhenCodeTokenRequestUsesUnauthorizedResourceSubset_ShouldReturnInvalidTarget()
    {
        var storage = factory.Services.GetRequiredService<IStorage>();
        var memoryStorage = factory.Services.GetRequiredService<MemoryStorage>();
        var suffix = CryptoRandom.CreateUniqueId(4, CryptoRandom.OutputFormat.Hex);
        var clientId = $"code-unauthorized-subset-client-{suffix}";
        memoryStorage.GetDemoRealmStore().Clients[clientId] = new Client
        {
            Realm = MemoryStorage.DemoRealm,
            Id = clientId,
            Name = "Code Unauthorized Subset Client",
            RequireClientSecret = false,
            RequirePkce = false,
            AllowedGrantTypes = ["authorization_code"],
            AllowedIdentityScopes = { "openid" },
            AllowedResourceServers = { "apiserver" },
            AllowedResponseTypes = { "code" },
            RedirectUris = { "http://localhost:5000/**" }
        };

        var resources = await storage.GetResourceStore(MemoryStorage.DemoRealm).FindRequestedResourcesAsync(
            ["openid"],
            ["https://api.demo.local/apiserver"],
            onlyEnabled: true);

        var code = new RoyalIdentity.Models.Tokens.AuthorizationCode(
            clientId,
            SubjectFactory.Create("alice", "Test Name", "admin"),
            "session",
            DateTime.UtcNow,
            300,
            resources,
            "http://localhost:5000/callback");

        await storage.GetAuthorizationCodeStore(MemoryStorage.DemoRealm).StoreAuthorizationCodeAsync(code, default);

        var client = factory.CreateClient();
        var url = Oidc.Routes.BuildTokenUrl(MemoryStorage.DemoRealm.Path);

        var response = await client.PostAsync(url,
            new FormUrlEncodedContent(
                new Dictionary<string, string>
                {
                    ["grant_type"] = "authorization_code",
                    ["code"] = code.Code,
                    ["client_id"] = clientId,
                    ["redirect_uri"] = "http://localhost:5000/callback",
                    ["resource"] = "https://unknown.example.test/resource"
                }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("invalid_target", body);
    }
}
