using Microsoft.Extensions.DependencyInjection;
using RoyalIdentity.Extensions;
using RoyalIdentity.Contracts.Storage;
using RoyalIdentity.Models;
using RoyalIdentity.Models.Scopes;
using RoyalIdentity.Utils;
using System.Net;
using System.Net.Http.Json;
using Tests.Integration.Prepare;

namespace Tests.Integration.Endpoints;

public class RefreshTokenTests : IClassFixture<AppFactory>
{
    private readonly AppFactory factory;

    public RefreshTokenTests(AppFactory factory)
    {
        this.factory = factory;
    }

    [Fact]
    public async Task Post_WhenValidRefreshToken_ShouldReturnNewTokens()
    {
        // Arrange
        var storage = factory.Services.GetRequiredService<MemoryStorage>();

        var clientId = "refresh_grant_type_client_3";
        storage.GetDemoRealmStore().Clients.TryAdd(clientId, new RoyalIdentity.Models.Client()
        {
            Realm = MemoryStorage.DemoRealm,
            Id = clientId,
            Name = "Demo Client",
            RequireClientSecret = false,
            AllowOfflineAccess = true,
            AllowedIdentityScopes = { "openid", "profile", "email" },
            AllowedResponseTypes = { "code" },
            AllowedGrantTypes = ["code", "refresh_token"],
            RedirectUris = { "http://localhost:5000/**", "https://localhost:5001/**" }
        });

        var client = factory.CreateClient();
        await client.LoginAliceAsync();
        var tokens = await client.GetTokensAsync(clientId: clientId);
        var refresh_token = tokens.RefreshToken!;

        var url = Oidc.Routes.BuildTokenUrl(MemoryStorage.DemoRealm.Path);

        // Act
        var response = await client.PostAsync(url,
            new FormUrlEncodedContent(
                new Dictionary<string, string>
                {
                    ["grant_type"] = "refresh_token",
                    ["refresh_token"] = refresh_token,
                    ["client_id"] = clientId,
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
    public async Task Post_WhenDoNotHasRefreshTokenGrantTypeAllowed_ShouldReturnBadRequest()
    {
        // Arrange — dedicated client: offline_access allowed, but "refresh_token" grant type is not
        var storage = factory.Services.GetRequiredService<MemoryStorage>();
        var clientId = "no_refresh_grant_type_client";
        storage.GetDemoRealmStore().Clients.TryAdd(clientId, new RoyalIdentity.Models.Client()
        {
            Realm = MemoryStorage.DemoRealm,
            Id = clientId,
            Name = "No Refresh Grant Client",
            RequireClientSecret = false,
            AllowOfflineAccess = true,
            AllowedIdentityScopes = { "openid", "profile" },
            AllowedResponseTypes = { "code" },
            AllowedGrantTypes = ["authorization_code"],
            RedirectUris = { "http://localhost:5000/**", "https://localhost:5001/**" }
        });

        var client = factory.CreateClient();
        await client.LoginAliceAsync();
        var tokens = await client.GetTokensAsync(clientId: clientId);
        var refreshToken = tokens.RefreshToken!;

        var url = Oidc.Routes.BuildTokenUrl(MemoryStorage.DemoRealm.Path);

        // Act
        var response = await client.PostAsync(url,
            new FormUrlEncodedContent(
                new Dictionary<string, string>
                {
                    ["grant_type"] = "refresh_token",
                    ["refresh_token"] = refreshToken,
                    ["client_id"] = clientId,
                }));

        var content = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        Assert.NotNull(content);
        Assert.True(content.ContainsKey("error_description"));
        Assert.Equal("Client not authorized for refresh_token flow", content["error_description"].ToString());
    }

    [Fact]
    public async Task Post_WhenClientHasSecret_And_SecretNotInformed_ShouldReturnBadRequest()
    {
        // Arrange
        var storage = factory.Services.GetRequiredService<MemoryStorage>();

        var clientId = "refresh_grant_type_client_1";
        var clientSecret = CryptoRandom.CreateUniqueId();
        var secretHash = clientSecret.Sha512();
        storage.GetDemoRealmStore().Clients.TryAdd(clientId, new RoyalIdentity.Models.Client()
        {
            Realm = MemoryStorage.DemoRealm,
            Id = clientId,
            Name = "Client with Secret",
            RequireClientSecret = true,
            RequirePkce = false,
            AllowOfflineAccess = true,
            AllowedIdentityScopes = { "openid", "profile", "email" },
            AllowedResponseTypes = { "code" },
            AllowedGrantTypes = ["code", "refresh_token"],
            RedirectUris = { "http://localhost:5000/**", "https://localhost:5001/**" },
            ClientSecrets = { new RoyalIdentity.Models.ClientSecret(secretHash) }
        });

        var client = factory.CreateClient();
        await client.LoginAliceAsync();
        var tokens = await client.GetTokensAsync(clientId: clientId);
        var refresh_token = tokens.RefreshToken!;

        var url = Oidc.Routes.BuildTokenUrl(MemoryStorage.DemoRealm.Path);

        // Act
        var response = await client.PostAsync(url,
            new FormUrlEncodedContent(
                new Dictionary<string, string>
                {
                    ["grant_type"] = "refresh_token",
                    ["refresh_token"] = refresh_token,
                    ["client_id"] = clientId,
                }));

        var content = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }


    [Fact]
    public async Task Post_WhenValidRefreshToken_And_ValidSecret_ShouldReturnNewTokens()
    {
        // Arrange
        var storage = factory.Services.GetRequiredService<MemoryStorage>();

        var clientId = "refresh_grant_type_client_2";
        var clientSecret = CryptoRandom.CreateUniqueId();
        var secretHash = clientSecret.Sha512();
        storage.GetDemoRealmStore().Clients.TryAdd(clientId, new RoyalIdentity.Models.Client()
        {
            Realm = MemoryStorage.DemoRealm,
            Id = clientId,
            Name = "Client with Secret",
            RequireClientSecret = true,
            RequirePkce = false,
            AllowOfflineAccess = true,
            AllowedIdentityScopes = { "openid", "profile", "email" },
            AllowedResponseTypes = { "code" },
            AllowedGrantTypes = [ "code", "refresh_token" ],
            RedirectUris = { "http://localhost:5000/**", "https://localhost:5001/**" },
            ClientSecrets = { new RoyalIdentity.Models.ClientSecret(secretHash) }
        });

        var client = factory.CreateClient();
        await client.LoginAliceAsync();
        var tokens = await client.GetTokensAsync(clientId: clientId);
        var refresh_token = tokens.RefreshToken!;

        var url = Oidc.Routes.BuildTokenUrl(MemoryStorage.DemoRealm.Path);

        // Act
        var response = await client.PostAsync(url,
            new FormUrlEncodedContent(
                new Dictionary<string, string>
                {
                    ["grant_type"] = "refresh_token",
                    ["refresh_token"] = refresh_token,
                    ["client_id"] = clientId,
                    ["client_secret"] = clientSecret
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
    public async Task Post_WhenRefreshTokenHasResourceIndicator_ShouldPreserveAudience()
    {
        var (clientId, refreshToken, _) = await CreateRefreshTokenWithResourcesAsync(
            ["https://api.demo.local/apiserver"]);

        var client = factory.CreateClient();
        var url = Oidc.Routes.BuildTokenUrl(MemoryStorage.DemoRealm.Path);

        var response = await client.PostAsync(url,
            new FormUrlEncodedContent(
                new Dictionary<string, string>
                {
                    ["grant_type"] = "refresh_token",
                    ["refresh_token"] = refreshToken,
                    ["client_id"] = clientId,
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
    public async Task Post_WhenRefreshTokenRequestsResourceSubset_ShouldSetSubsetAudience()
    {
        var ordersResource = AddOrdersResourceServer();
        var (clientId, refreshToken, _) = await CreateRefreshTokenWithResourcesAsync(
            ["https://api.demo.local/apiserver", ordersResource]);

        var client = factory.CreateClient();
        var url = Oidc.Routes.BuildTokenUrl(MemoryStorage.DemoRealm.Path);

        var response = await client.PostAsync(url,
            new FormUrlEncodedContent(
                new Dictionary<string, string>
                {
                    ["grant_type"] = "refresh_token",
                    ["refresh_token"] = refreshToken,
                    ["client_id"] = clientId,
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
    public async Task Post_WhenRefreshTokenRequestsResourceSubsetWithApiScopes_ShouldDownscopeScopesAndAudience()
    {
        var storage = factory.Services.GetRequiredService<MemoryStorage>();
        var suffix = CryptoRandom.CreateUniqueId(4, CryptoRandom.OutputFormat.Hex);
        var ordersServer = $"orders-refresh-with-scope-{suffix}";
        var ordersScope = $"orders:read:{suffix}";
        var ordersResource = $"https://orders.demo.local/{suffix}";
        storage.GetDemoRealmStore().ResourceServers[ordersServer] = new ResourceServer(
            ScopeVisibility.Public,
            ordersServer,
            "Orders API",
            "Orders API")
        {
            Scopes =
            [
                new Scope(ScopeVisibility.Public, ordersScope, "Orders read", "Read orders")
            ],
            ProtectedResources =
            [
                new ProtectedResource(ordersResource)
            ]
        };

        var (clientId, refreshToken, _) = await CreateRefreshTokenWithResourcesAsync(
            ["https://api.demo.local/apiserver", ordersResource],
            ["openid", "offline_access", "api:read", ordersScope]);

        var client = factory.CreateClient();
        var url = Oidc.Routes.BuildTokenUrl(MemoryStorage.DemoRealm.Path);

        var response = await client.PostAsync(url,
            new FormUrlEncodedContent(
                new Dictionary<string, string>
                {
                    ["grant_type"] = "refresh_token",
                    ["refresh_token"] = refreshToken,
                    ["client_id"] = clientId,
                    ["resource"] = ordersResource
                }));

        var content = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(content);
        var scope = content!["scope"].ToString()!;
        Assert.Contains(ordersScope, scope);
        // identity scope and offline_access survive the downscope (scope axis, not the resource axis)
        Assert.Contains("openid", scope);
        Assert.Contains("offline_access", scope);
        Assert.DoesNotContain("api:read", scope);
        var accessToken = content["access_token"].ToString()!;
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(accessToken);
        Assert.Contains(ordersResource, jwt.Audiences);
        Assert.DoesNotContain("https://api.demo.local/apiserver", jwt.Audiences);
    }

    [Fact]
    public async Task Post_WhenRefreshTokenRequestsUnauthorizedResourceSubset_ShouldReturnInvalidTarget()
    {
        var ordersResource = AddOrdersResourceServer();
        var (clientId, refreshToken, _) = await CreateRefreshTokenWithResourcesAsync(
            ["https://api.demo.local/apiserver"]);

        var client = factory.CreateClient();
        var url = Oidc.Routes.BuildTokenUrl(MemoryStorage.DemoRealm.Path);

        var response = await client.PostAsync(url,
            new FormUrlEncodedContent(
                new Dictionary<string, string>
                {
                    ["grant_type"] = "refresh_token",
                    ["refresh_token"] = refreshToken,
                    ["client_id"] = clientId,
                    ["resource"] = ordersResource
                }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("invalid_target", body);
    }

    private string AddOrdersResourceServer()
    {
        var storage = factory.Services.GetRequiredService<MemoryStorage>();
        var suffix = CryptoRandom.CreateUniqueId(4, CryptoRandom.OutputFormat.Hex);
        var ordersServer = $"orders-{suffix}";
        var ordersResource = $"https://orders.demo.local/{suffix}";

        storage.GetDemoRealmStore().ResourceServers[ordersServer] = new ResourceServer(
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

        return ordersResource;
    }

    private async Task<(string ClientId, string RefreshToken, string AccessToken)> CreateRefreshTokenWithResourcesAsync(
        string[] resourceUris,
        string[]? scopeNames = null)
    {
        var memoryStorage = factory.Services.GetRequiredService<MemoryStorage>();
        var storage = factory.Services.GetRequiredService<IStorage>();
        var store = memoryStorage.GetDemoRealmStore();
        var suffix = CryptoRandom.CreateUniqueId(4, CryptoRandom.OutputFormat.Hex);
        var clientId = $"refresh-resource-client-{suffix}";

        var allowedResourceServers = store.ResourceServers.Values
            .Where(server => server.ProtectedResources.Any(resource => resourceUris.Contains(resource.ResourceUri)))
            .Select(server => server.Name)
            .ToArray();

        var resourceClient = new Client
        {
            Realm = MemoryStorage.DemoRealm,
            Id = clientId,
            Name = "Refresh Resource Client",
            RequireClientSecret = false,
            RequirePkce = false,
            AllowOfflineAccess = true,
            AllowedGrantTypes = ["authorization_code", "refresh_token"],
            AllowedIdentityScopes = { "openid" },
            AllowedResponseTypes = { "code" },
            RedirectUris = { "http://localhost:5000/**" }
        };
        resourceClient.AllowedResourceServers.AddRange(allowedResourceServers);
        store.Clients[clientId] = resourceClient;

        var resources = await storage.GetResourceStore(MemoryStorage.DemoRealm).FindRequestedResourcesAsync(
            scopeNames ?? ["openid", "offline_access"],
            resourceUris,
            onlyEnabled: true);

        var code = new RoyalIdentity.Models.Tokens.AuthorizationCode(
            clientId,
            SubjectFactory.CreateWithSession(storage, MemoryStorage.DemoRealm, MemoryStorage.AliceSubjectId, "Test Name", "admin"),
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

        return (
            clientId,
            content!["refresh_token"].ToString()!,
            content["access_token"].ToString()!);
    }
}
