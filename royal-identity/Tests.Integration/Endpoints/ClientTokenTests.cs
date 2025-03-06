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
            AllowedScopes = { "openid", "profile", "email", "api" },
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
}