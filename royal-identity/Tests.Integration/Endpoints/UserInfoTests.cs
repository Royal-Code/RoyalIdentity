using System.Net.Http.Json;
using Tests.Integration.Prepare;

namespace Tests.Integration.Endpoints;

public class UserInfoTests : IClassFixture<AppFactory>
{
    private readonly AppFactory factory;

    public UserInfoTests(AppFactory factory)
    {
        this.factory = factory;
    }

    [Fact]
    public async Task Get_Must_ReturnTheUserInfo()
    {
        // Arrange
        var client = factory.CreateClient();
        await client.LoginAliceAsync();
        var tokens = await client.GetTokensAsync();
        var access_token = tokens.AccessToken;
        var url = Oidc.Routes.BuildUserInfoUrl(MemoryStorage.DemoRealm.Path);

        // Act
        var message = new HttpRequestMessage(HttpMethod.Get, url);
        message.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", access_token);
        var response = await client.SendAsync(message);
        var content = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();

        // Assert
        Assert.True(response.IsSuccessStatusCode);
        Assert.NotNull(content);

        Assert.Contains("sub", content);
        Assert.Contains("name", content);
    }
}
