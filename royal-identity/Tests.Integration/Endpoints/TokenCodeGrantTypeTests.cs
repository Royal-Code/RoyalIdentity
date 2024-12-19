using Microsoft.Extensions.DependencyInjection;
using RoyalIdentity.Contracts.Storage;
using RoyalIdentity.Utils;
using System.Net;
using System.Text.Json;
using Tests.Integration.Prepare;

namespace Tests.Integration.Endpoints;

public class TokenCodeGrantTypeTests : IClassFixture<AppFactory>
{
    private static readonly string[] scopeNames = ["openid", "profile"];

    private readonly AppFactory factory;

    public TokenCodeGrantTypeTests(AppFactory factory)
    {
        this.factory = factory;
    }

    [Fact]
    public async Task Post_WhenValidCode_Must_GenerateToken()
    {
        // Arrange
        var codeStore = factory.Services.GetRequiredService<IAuthorizationCodeStore>();
        var resourcesStore = factory.Services.GetRequiredService<IResourceStore>();

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

        // Act
        var response = await client.PostAsync("/connect/token", 
            new FormUrlEncodedContent(
                new Dictionary<string, string>
                {
                    ["grant_type"] = "authorization_code",
                    ["code"] = code.Code,
                    ["client_id"] = "demo_client",
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
    public async Task Post_WhenValidCode_WithPkce_Must_GenerateToken()
    {
        // Arrange
        var codeStore = factory.Services.GetRequiredService<IAuthorizationCodeStore>();
        var resourcesStore = factory.Services.GetRequiredService<IResourceStore>();

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

        // Act
        var response = await client.PostAsync("/connect/token",
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
}
