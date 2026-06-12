using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using RoyalIdentity.Extensions;
using RoyalIdentity.Storage.InMemory;
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
}