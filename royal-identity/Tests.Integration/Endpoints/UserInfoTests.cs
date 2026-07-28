using System.Net.Http.Json;
using Tests.Integration.Prepare;

namespace Tests.Integration.Endpoints;

public class UserInfoTests : IClassFixture<PersistentStorageAppFactory>
{
    private readonly PersistentStorageAppFactory factory;

    public UserInfoTests(PersistentStorageAppFactory factory)
    {
        this.factory = factory;
    }

    [Fact]
    public async Task Get_Must_ReturnTheUserInfo()
    {
        // Arrange
        var client = factory.CreateClient();
        await client.LoginAsync(factory.Handles.Demo, factory.Handles.Alice);
        var tokens = await client.GetTokensAsync(
            factory.Handles.Demo,
            factory.Handles.DemoClient);
        var access_token = tokens.AccessToken;
        var url = Oidc.Routes.BuildUserInfoUrl(factory.Handles.Demo.Path);

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
