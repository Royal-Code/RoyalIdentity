using Microsoft.Extensions.DependencyInjection;
using RoyalIdentity.Extensions;
using RoyalIdentity.Storage.InMemory;
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
        var client = factory.CreateClient();
        await client.LoginAliceAsync();
        var tokens = await client.GetTokensAsync();
        var refresh_token = tokens.RefreshToken!;

        // Act
        var response = await client.PostAsync("/connect/token",
            new FormUrlEncodedContent(
                new Dictionary<string, string>
                {
                    ["grant_type"] = "refresh_token",
                    ["refresh_token"] = refresh_token,
                    ["client_id"] = "demo_client",
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
    public async Task Post_WhenClientHasSecret_And_SecretNotInformed_ShouldReturnBadRequest()
    {
        // Arrange
        var storage = factory.Services.GetRequiredService<MemoryStorage>();

        var clientId = "refresh_grant_type_client_1";
        var clientSecret = CryptoRandom.CreateUniqueId();
        var secretHash = clientSecret.Sha512();
        storage.Clients.TryAdd(clientId, new RoyalIdentity.Models.Client()
        {
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

        var client = factory.CreateClient();
        await client.LoginAliceAsync();
        var tokens = await client.GetTokensAsync(clientId: clientId);
        var refresh_token = tokens.RefreshToken!;

        // Act
        var response = await client.PostAsync("/connect/token",
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
        storage.Clients.TryAdd(clientId, new RoyalIdentity.Models.Client()
        {
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

        var client = factory.CreateClient();
        await client.LoginAliceAsync();
        var tokens = await client.GetTokensAsync(clientId: clientId);
        var refresh_token = tokens.RefreshToken!;

        // Act
        var response = await client.PostAsync("/connect/token",
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
}
