using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using RoyalIdentity.Models;
using RoyalIdentity.Extensions;
using RoyalIdentity.Models.Scopes;
using RoyalIdentity.Storage.InMemory;
using RoyalIdentity.Utils;
using Tests.Integration.Prepare;

namespace Tests.Integration.Endpoints;

public class ClientTokenTests : IClassFixture<AppFactory>
{
    private readonly AppFactory factory;

    public ClientTokenTests(AppFactory factory)
    {
        this.factory = factory;
    }

    [Fact]
    public async Task Post_WhenValidClientCredentials_ShouldReturnNewTokens()
    {
        // Arrange
        var storage = factory.Services.GetRequiredService<MemoryStorage>();

        var clientId = "client_credentials_client_1";
        var clientSecret = "client_credentials_client_1_secret";
        var secretHash = clientSecret.Sha512();
        storage.GetDemoRealmStore().Clients.TryAdd(clientId, new RoyalIdentity.Models.Client()
        {
            Realm = MemoryStorage.DemoRealm,
            Id = clientId,
            Name = "Demo Client",
            RequireClientSecret = true,
            AllowOfflineAccess = false,
            AllowedIdentityScopes = { "openid", "profile", "email" },
            AllowedScopes = { "api" },
            AllowedResponseTypes = { "code" },
            AllowedGrantTypes = ["client_credentials"],
            RedirectUris = { "http://localhost:5000/**", "https://localhost:5001/**" },
            ClientSecrets = { new RoyalIdentity.Models.ClientSecret(secretHash) }
        });

        var client = factory.CreateClient();
        var url = Oidc.Routes.BuildTokenUrl(MemoryStorage.DemoRealm.Path);

        // Act
        var response = await client.PostAsync(url,
            new FormUrlEncodedContent(
                new Dictionary<string, string>
                {
                    ["grant_type"] = "client_credentials",
                    ["client_id"] = clientId,
                    ["client_secret"] = clientSecret,
                    ["scope"] = "api"
                }));

        var content = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        Assert.NotNull(content);
        Assert.True(content.ContainsKey("access_token"));
        Assert.True(content.ContainsKey("token_type"));
        Assert.True(content.ContainsKey("expires_in"));
        Assert.True(content.ContainsKey("scope"));
        Assert.False(content.ContainsKey("id_token"));
    }

    [Fact]
    public async Task Post_WhenAllowAllResourceServers_ShouldAuthorizeAnyScope()
    {
        // Full Scope Allowed (ADR-011): the client lists no scope/resource server, only AllowAllResourceServers.
        var storage = factory.Services.GetRequiredService<MemoryStorage>();

        var clientId = "full_scope_client";
        var clientSecret = "full_scope_client_secret";
        var secretHash = clientSecret.Sha512();
        storage.GetDemoRealmStore().Clients.TryAdd(clientId, new RoyalIdentity.Models.Client()
        {
            Realm = MemoryStorage.DemoRealm,
            Id = clientId,
            Name = "Full Scope Client",
            ClientType = RoyalIdentity.Models.ClientType.Confidential,
            RequireClientSecret = true,
            AllowAllResourceServers = true,
            AllowedResponseTypes = { "code" },
            AllowedGrantTypes = ["client_credentials"],
            ClientSecrets = { new RoyalIdentity.Models.ClientSecret(secretHash) }
        });

        var client = factory.CreateClient();
        var url = Oidc.Routes.BuildTokenUrl(MemoryStorage.DemoRealm.Path);

        var response = await client.PostAsync(url,
            new FormUrlEncodedContent(
                new Dictionary<string, string>
                {
                    ["grant_type"] = "client_credentials",
                    ["client_id"] = clientId,
                    ["client_secret"] = clientSecret,
                    ["scope"] = "api:read api:write"
                }));

        var content = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(content);
        Assert.True(content.ContainsKey("access_token"));
        var scope = content!["scope"].ToString();
        Assert.Contains("api:read", scope);
        Assert.Contains("api:write", scope);
    }

    [Fact]
    public async Task Post_WhenScopeNotAllowed_ShouldReturnInvalidScope()
    {
        // The client is allowed no API scope and has no Full Scope Allowed: requesting "api" is invalid_scope.
        var storage = factory.Services.GetRequiredService<MemoryStorage>();

        var clientId = "no_api_client";
        var clientSecret = "no_api_client_secret";
        var secretHash = clientSecret.Sha512();
        storage.GetDemoRealmStore().Clients.TryAdd(clientId, new RoyalIdentity.Models.Client()
        {
            Realm = MemoryStorage.DemoRealm,
            Id = clientId,
            Name = "No Api Client",
            ClientType = RoyalIdentity.Models.ClientType.Confidential,
            RequireClientSecret = true,
            AllowedResponseTypes = { "code" },
            AllowedGrantTypes = ["client_credentials"],
            ClientSecrets = { new RoyalIdentity.Models.ClientSecret(secretHash) }
        });

        var client = factory.CreateClient();
        var url = Oidc.Routes.BuildTokenUrl(MemoryStorage.DemoRealm.Path);

        var response = await client.PostAsync(url,
            new FormUrlEncodedContent(
                new Dictionary<string, string>
                {
                    ["grant_type"] = "client_credentials",
                    ["client_id"] = clientId,
                    ["client_secret"] = clientSecret,
                    ["scope"] = "api"
                }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("invalid_scope", body);
    }

    [Fact]
    public async Task Post_WhenClientCredentialsRequestsOfflineAccess_ShouldReturnInvalidScope()
    {
        var storage = factory.Services.GetRequiredService<MemoryStorage>();

        var clientId = $"offline-client-{CryptoRandom.CreateUniqueId(6)}";
        var clientSecret = CryptoRandom.CreateUniqueId();
        storage.GetDemoRealmStore().Clients[clientId] = new Client
        {
            Realm = MemoryStorage.DemoRealm,
            Id = clientId,
            Name = "Offline Client Credentials Client",
            ClientType = ClientType.Confidential,
            RequireClientSecret = true,
            AllowOfflineAccess = true,
            AllowedScopes = { "api" },
            AllowedGrantTypes = ["client_credentials"],
            ClientSecrets = { new ClientSecret(clientSecret.Sha512()) }
        };

        var client = factory.CreateClient();
        var url = Oidc.Routes.BuildTokenUrl(MemoryStorage.DemoRealm.Path);

        var response = await client.PostAsync(url,
            new FormUrlEncodedContent(
                new Dictionary<string, string>
                {
                    ["grant_type"] = "client_credentials",
                    ["client_id"] = clientId,
                    ["client_secret"] = clientSecret,
                    ["scope"] = "api offline_access"
                }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("invalid_scope", body);
        Assert.DoesNotContain("access_token", body);
    }

    [Fact]
    public async Task Post_WhenClientCredentialsOmitsScope_ShouldReturnDefaultAllowedScopes()
    {
        var storage = factory.Services.GetRequiredService<MemoryStorage>();

        var clientId = $"default-scope-client-{CryptoRandom.CreateUniqueId(6)}";
        var clientSecret = CryptoRandom.CreateUniqueId();
        storage.GetDemoRealmStore().Clients[clientId] = new Client
        {
            Realm = MemoryStorage.DemoRealm,
            Id = clientId,
            Name = "Default Scope Client",
            ClientType = ClientType.Confidential,
            RequireClientSecret = true,
            AllowedScopes = { "api" },
            AllowedGrantTypes = ["client_credentials"],
            ClientSecrets = { new ClientSecret(clientSecret.Sha512()) }
        };

        var client = factory.CreateClient();
        var url = Oidc.Routes.BuildTokenUrl(MemoryStorage.DemoRealm.Path);

        var response = await client.PostAsync(url,
            new FormUrlEncodedContent(
                new Dictionary<string, string>
                {
                    ["grant_type"] = "client_credentials",
                    ["client_id"] = clientId,
                    ["client_secret"] = clientSecret
                }));

        var content = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(content);
        Assert.True(content.ContainsKey("access_token"));
        Assert.Equal("api", content["scope"].ToString());
    }

    [Fact]
    public async Task Post_WithResourceIndicator_ShouldSetAudienceToResourceUri()
    {
        // RFC 8707: requesting a resource indicator emits its URI as the aud and suppresses the legacy RS audience.
        var storage = factory.Services.GetRequiredService<MemoryStorage>();

        var clientId = $"resource-client-{CryptoRandom.CreateUniqueId(6)}";
        var clientSecret = CryptoRandom.CreateUniqueId();
        storage.GetDemoRealmStore().Clients[clientId] = new Client
        {
            Realm = MemoryStorage.DemoRealm,
            Id = clientId,
            Name = "Resource Client",
            ClientType = ClientType.Confidential,
            RequireClientSecret = true,
            AllowedResourceServers = { "apiserver" },
            AllowedGrantTypes = ["client_credentials"],
            ClientSecrets = { new ClientSecret(clientSecret.Sha512()) }
        };

        var client = factory.CreateClient();
        var url = Oidc.Routes.BuildTokenUrl(MemoryStorage.DemoRealm.Path);

        var response = await client.PostAsync(url,
            new FormUrlEncodedContent(
                new Dictionary<string, string>
                {
                    ["grant_type"] = "client_credentials",
                    ["client_id"] = clientId,
                    ["client_secret"] = clientSecret,
                    ["scope"] = "api",
                    ["resource"] = "https://api.demo.local/apiserver"
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
    public async Task Post_WithUnknownResourceIndicator_ShouldReturnInvalidTarget()
    {
        var storage = factory.Services.GetRequiredService<MemoryStorage>();

        var clientId = $"unknown-resource-client-{CryptoRandom.CreateUniqueId(6)}";
        var clientSecret = CryptoRandom.CreateUniqueId();
        storage.GetDemoRealmStore().Clients[clientId] = new Client
        {
            Realm = MemoryStorage.DemoRealm,
            Id = clientId,
            Name = "Unknown Resource Client",
            ClientType = ClientType.Confidential,
            RequireClientSecret = true,
            AllowedResourceServers = { "apiserver" },
            AllowedGrantTypes = ["client_credentials"],
            ClientSecrets = { new ClientSecret(clientSecret.Sha512()) }
        };

        var client = factory.CreateClient();
        var url = Oidc.Routes.BuildTokenUrl(MemoryStorage.DemoRealm.Path);

        var response = await client.PostAsync(url,
            new FormUrlEncodedContent(
                new Dictionary<string, string>
                {
                    ["grant_type"] = "client_credentials",
                    ["client_id"] = clientId,
                    ["client_secret"] = clientSecret,
                    ["scope"] = "api",
                    ["resource"] = "https://unknown.example/api"
                }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("invalid_target", body);
    }

    [Fact]
    public async Task Post_WithResourceNotAllowed_ShouldReturnInvalidTarget()
    {
        // Audience-only request for a resource whose resource server is not in AllowedResourceServers.
        var storage = factory.Services.GetRequiredService<MemoryStorage>();

        var clientId = $"no-resource-client-{CryptoRandom.CreateUniqueId(6)}";
        var clientSecret = CryptoRandom.CreateUniqueId();
        storage.GetDemoRealmStore().Clients[clientId] = new Client
        {
            Realm = MemoryStorage.DemoRealm,
            Id = clientId,
            Name = "No Resource Client",
            ClientType = ClientType.Confidential,
            RequireClientSecret = true,
            AllowedGrantTypes = ["client_credentials"],
            ClientSecrets = { new ClientSecret(clientSecret.Sha512()) }
        };

        var client = factory.CreateClient();
        var url = Oidc.Routes.BuildTokenUrl(MemoryStorage.DemoRealm.Path);

        var response = await client.PostAsync(url,
            new FormUrlEncodedContent(
                new Dictionary<string, string>
                {
                    ["grant_type"] = "client_credentials",
                    ["client_id"] = clientId,
                    ["client_secret"] = clientSecret,
                    ["resource"] = "https://api.demo.local/apiserver"
                }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("invalid_target", body);
    }

    [Fact]
    public async Task Post_WithMultipleResourceIndicators_ShouldSetAllResourceAudiences()
    {
        var storage = factory.Services.GetRequiredService<MemoryStorage>();
        var store = storage.GetDemoRealmStore();
        var suffix = CryptoRandom.CreateUniqueId(4, CryptoRandom.OutputFormat.Hex);
        var ordersServer = $"orders-{suffix}";
        var ordersScope = $"orders:read:{suffix}";
        var ordersResource = $"https://orders.demo.local/{suffix}";

        store.ResourceServers[ordersServer] = new ResourceServer(
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

        var clientId = $"multi-resource-client-{suffix}";
        var clientSecret = CryptoRandom.CreateUniqueId();
        store.Clients[clientId] = new Client
        {
            Realm = MemoryStorage.DemoRealm,
            Id = clientId,
            Name = "Multi Resource Client",
            ClientType = ClientType.Confidential,
            RequireClientSecret = true,
            AllowedResourceServers = { "apiserver", ordersServer },
            AllowedGrantTypes = ["client_credentials"],
            ClientSecrets = { new ClientSecret(clientSecret.Sha512()) }
        };

        var client = factory.CreateClient();
        var url = Oidc.Routes.BuildTokenUrl(MemoryStorage.DemoRealm.Path);

        var response = await client.PostAsync(url,
            new FormUrlEncodedContent(
            [
                new("grant_type", "client_credentials"),
                new("client_id", clientId),
                new("client_secret", clientSecret),
                new("resource", "https://api.demo.local/apiserver"),
                new("resource", ordersResource),
            ]));

        var content = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(content);
        var accessToken = content!["access_token"].ToString()!;
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(accessToken);
        Assert.Contains("https://api.demo.local/apiserver", jwt.Audiences);
        Assert.Contains(ordersResource, jwt.Audiences);
        Assert.DoesNotContain("apiserver", jwt.Audiences);
        Assert.DoesNotContain(ordersServer, jwt.Audiences);
    }

    [Fact]
    public async Task Post_WithScopeAndResourceFromDifferentResourceCapableServer_ShouldReturnInvalidTarget()
    {
        var storage = factory.Services.GetRequiredService<MemoryStorage>();
        var store = storage.GetDemoRealmStore();
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

        var clientId = $"mismatch-resource-client-{suffix}";
        var clientSecret = CryptoRandom.CreateUniqueId();
        store.Clients[clientId] = new Client
        {
            Realm = MemoryStorage.DemoRealm,
            Id = clientId,
            Name = "Mismatch Resource Client",
            ClientType = ClientType.Confidential,
            RequireClientSecret = true,
            AllowedResourceServers = { "apiserver", ordersServer },
            AllowedGrantTypes = ["client_credentials"],
            ClientSecrets = { new ClientSecret(clientSecret.Sha512()) }
        };

        var client = factory.CreateClient();
        var url = Oidc.Routes.BuildTokenUrl(MemoryStorage.DemoRealm.Path);

        var response = await client.PostAsync(url,
            new FormUrlEncodedContent(
                new Dictionary<string, string>
                {
                    ["grant_type"] = "client_credentials",
                    ["client_id"] = clientId,
                    ["client_secret"] = clientSecret,
                    ["scope"] = "api",
                    ["resource"] = ordersResource
                }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("invalid_target", body);
    }
}
