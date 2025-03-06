using System.Net;
using Tests.Integration.Prepare;

namespace Tests.Integration.Endpoints;

public class RevocationTests : IClassFixture<AppFactory>
{
    private readonly AppFactory factory;

    public RevocationTests(AppFactory factory)
    {
        this.factory = factory;
    }

    [Fact]
    public async Task Post_WhenValidAccessToken_MustRevoked()
    {
        // Arrange
        var client = factory.CreateClient();
        await client.LoginAliceAsync();
        var tokens = await client.GetTokensAsync();
        var access_token = tokens.AccessToken;
        var url = Oidc.Routes.BuildRevocationUrl(MemoryStorage.DemoRealm.Path);

        // Act
        var response = await client.PostAsync(url,
            new FormUrlEncodedContent(
                new Dictionary<string, string>
                {
                    ["token"] = access_token,
                    ["client_id"] = "demo_client",
                }));

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        Assert.Empty(content);
    }

    [Fact]
    public async Task Post_WhenValidRefreshToken_MustRevoked()
    {
        // Arrange
        var client = factory.CreateClient();
        await client.LoginAliceAsync();
        var tokens = await client.GetTokensAsync();
        var refresh_token = tokens.RefreshToken!;
        var url = Oidc.Routes.BuildRevocationUrl(MemoryStorage.DemoRealm.Path);

        // Act
        var response = await client.PostAsync(url,
            new FormUrlEncodedContent(
                new Dictionary<string, string>
                {
                    ["token"] = refresh_token,
                    ["client_id"] = "demo_client",
                }));

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Empty(content);
    }

    //token_type_hint

    [Fact]
    public async Task Post_WhenValidAccessToken_And_HintAccessToken_MustRevoked()
    {
        // Arrange
        var client = factory.CreateClient();
        await client.LoginAliceAsync();
        var tokens = await client.GetTokensAsync();
        var access_token = tokens.AccessToken;
        var url = Oidc.Routes.BuildRevocationUrl(MemoryStorage.DemoRealm.Path);

        // Act
        var response = await client.PostAsync(url,
            new FormUrlEncodedContent(
                new Dictionary<string, string>
                {
                    ["token"] = access_token,
                    ["client_id"] = "demo_client",
                    ["token_type_hint"] = "access_token",
                }));

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Empty(content);
    }

    [Fact]
    public async Task Post_WhenValidRefreshToken_And_HintRefreshToken_MustRevoked()
    {
        // Arrange
        var client = factory.CreateClient();
        await client.LoginAliceAsync();
        var tokens = await client.GetTokensAsync();
        var refresh_token = tokens.RefreshToken!;
        var url = Oidc.Routes.BuildRevocationUrl(MemoryStorage.DemoRealm.Path);

        // Act
        var response = await client.PostAsync(url,
            new FormUrlEncodedContent(
                new Dictionary<string, string>
                {
                    ["token"] = refresh_token,
                    ["client_id"] = "demo_client",
                    ["token_type_hint"] = "refresh_token",
                }));

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Empty(content);
    }

    [Fact]
    public async Task Post_WhenValidAccessToken_And_HintRefreshToken_MustOk()
    {
        // Arrange
        var client = factory.CreateClient();
        await client.LoginAliceAsync();
        var tokens = await client.GetTokensAsync();
        var access_token = tokens.AccessToken;
        var url = Oidc.Routes.BuildRevocationUrl(MemoryStorage.DemoRealm.Path);

        // Act
        var response = await client.PostAsync(url,
            new FormUrlEncodedContent(
                new Dictionary<string, string>
                {
                    ["token"] = access_token,
                    ["client_id"] = "demo_client",
                    ["token_type_hint"] = "refresh_token",
                }));

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Empty(content);
    }

    [Fact]
    public async Task Post_WhenValidRefreshToken_HintAccessToken_MustOk()
    {
        // Arrange
        var client = factory.CreateClient();
        await client.LoginAliceAsync();
        var tokens = await client.GetTokensAsync();
        var refresh_token = tokens.RefreshToken!;
        var url = Oidc.Routes.BuildRevocationUrl(MemoryStorage.DemoRealm.Path);

        // Act
        var response = await client.PostAsync(url,
            new FormUrlEncodedContent(
                new Dictionary<string, string>
                {
                    ["token"] = refresh_token,
                    ["client_id"] = "demo_client",
                    ["token_type_hint"] = "access_token",
                }));

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Empty(content);
    }

    [Fact]
    public async Task Post_WhenInvalidAccessToken_MustOk()
    {
        // Arrange
        var client = factory.CreateClient();
        var access_token = "AAA";
        var url = Oidc.Routes.BuildRevocationUrl(MemoryStorage.DemoRealm.Path);

        // Act
        var response = await client.PostAsync(url,
            new FormUrlEncodedContent(
                new Dictionary<string, string>
                {
                    ["token"] = access_token,
                    ["client_id"] = "demo_client",
                }));

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Empty(content);
    }

    [Fact]
    public async Task Post_WhenInvalidRefreshToken_MustOk()
    {
        // Arrange
        var client = factory.CreateClient();
        var refresh_token = "AAA";
        var url = Oidc.Routes.BuildRevocationUrl(MemoryStorage.DemoRealm.Path);

        // Act
        var response = await client.PostAsync(url,
            new FormUrlEncodedContent(
                new Dictionary<string, string>
                {
                    ["token"] = refresh_token,
                    ["client_id"] = "demo_client",
                }));

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Empty(content);
    }
}
