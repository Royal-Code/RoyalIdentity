using System.Text.Json;

namespace Tests.Integration.Prepare;

public class LoginTests : IClassFixture<AppFactory>
{
    private readonly AppFactory factory;
    
    public LoginTests(AppFactory factory)
    {
        this.factory = factory;
    }

    [Fact]
    public async Task Login_Profile()
    {
        // Arrange
        var client = factory.CreateClient();

        // Act
        await client.LoginAliceAsync();
        var response = await client.GetAsync("demo/test/account/profile");

        // Assert
        Assert.NotNull(response);
        Assert.True(response.IsSuccessStatusCode);

        var content = await response.Content.ReadAsStringAsync();
        Assert.NotNull(content);

        var subject = JsonSerializer.Deserialize<Dictionary<string, object>>(content);

        Assert.NotNull(subject);

        // The profile endpoint now returns the lean edge Subject (subjectId/displayName/isActive); the
        // username is no longer part of the borda. The sub is the stable SubjectId, not the username.
        Assert.Equal(MemoryStorage.AliceSubjectId, subject["subjectId"].ToString());
        Assert.Equal("Alice", subject["displayName"].ToString());
    }

    [Fact]
    public async Task Login_Logout()
    {
        // Arrange
        var client = factory.CreateClient();

        // Act
        await LoginExtensions.LoginAliceAsync(client);
        var response = await client.LogoutAsync();

        // Assert
        Assert.NotNull(response);
        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Login_GetToken()
    {
        // Arrange
        var client = factory.CreateClient();
        
        // Act
        await client.LoginAliceAsync();
        var token = await client.GetTokensAsync("demo_client", "openid profile offline_access");

        // Assert
        Assert.NotNull(token);
        Assert.NotNull(token.AccessToken);
        Assert.NotNull(token.TokenType);
        Assert.NotEqual(0, token.ExpiresIn);
        Assert.NotNull(token.RefreshToken);
        Assert.NotNull(token.IdentityToken);
    }
}
