// Ignore Spelling: Pkce

using Microsoft.Extensions.DependencyInjection;
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
        var codeStore = storage.AuthorizationCodes;
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
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

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
        var codeStore = storage.AuthorizationCodes;
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
        var codeStore = storage.AuthorizationCodes;
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
        var codeStore = storage.AuthorizationCodes;
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
        var codeStore = storage.AuthorizationCodes;
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
            AllowedScopes = { "openid", "profile", "email" },
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
        var codeStore = storage.AuthorizationCodes;
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
            AllowedScopes = { "openid", "profile", "email" },
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
}
