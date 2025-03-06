using HtmlAgilityPack;
using System.Net;
using System.Text.Json;
using Tests.Integration.Prepare;

namespace Tests.Integration.UI;

public class LoginPageTests : IClassFixture<AppFactory>
{
    private readonly AppFactory factory;

    public LoginPageTests(AppFactory factory)
    {
        this.factory = factory;
    }

    [Fact]
    public async Task Login_WhenValidCredentials_ShouldRedirectToRequestedOrigin()
    {
        // Arrange
        var client = factory.CreateClient();

        // will redirect to login page
        var loginPage = await client.GetAsync("/demo/test/protected-resource");
        var content = await loginPage.Content.ReadAsStringAsync();

        var doc = new HtmlDocument();
        doc.LoadHtml(content);
        
        var form = doc.DocumentNode.SelectSingleNode("//form");
        var formAction = new FormAction(client, form)
            .SetValue("Input.Username", "alice")
            .SetValue("Input.Password", "alice");

        // Act

        // after login, will redirect to protected resource
        var response = await formAction.SubmitAsync();

        content = await response.Content.ReadAsStringAsync();
        var people = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(content);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(people);
        Assert.Equal(6, people.Count);
        Assert.NotNull(people[0]["id"]);
        Assert.NotNull(people[0]["name"]);
    }

    [Fact]
    public async Task Login_WhenInvalidCredentials_ShouldReturnLoginPage()
    {
        // Arrange
        var client = factory.CreateClient();

        // will redirect to login page
        var loginPage = await client.GetAsync("/demo/test/protected-resource");
        var content = await loginPage.Content.ReadAsStringAsync();

        var doc = new HtmlDocument();
        doc.LoadHtml(content);

        var form = doc.DocumentNode.SelectSingleNode("//form");
        var formAction = new FormAction(client, form)
            .SetValue("Input.Username", "alice")
            .SetValue("Input.Password", "wrong");

        // Act

        // after login, will redirect to protected resource
        var response = await formAction.SubmitAsync();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var responseContent = await response.Content.ReadAsStringAsync();
        var responseDoc = new HtmlDocument();
        responseDoc.LoadHtml(responseContent);

        var error = responseDoc.DocumentNode.SelectSingleNode("//div[@class='alert alert-danger']");
        Assert.NotNull(error);
        Assert.Equal("Invalid username or password", error.InnerText.Trim());
    }
}
