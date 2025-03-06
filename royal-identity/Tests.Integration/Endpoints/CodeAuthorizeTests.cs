// Ignore Spelling: Pkce

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using RoyalIdentity.Extensions;
using RoyalIdentity.Storage.InMemory;
using System.Net;
using System.Web;
using Tests.Integration.Prepare;

namespace Tests.Integration.Endpoints;

public class CodeAuthorizeTests : IClassFixture<AppFactory>
{
    private readonly AppFactory factory;

    public CodeAuthorizeTests(AppFactory factory)
    {
        this.factory = factory;
    }

    [Fact]
    public async Task Get_Must_RedirectToLoginPage()
    {
        // Arrange
        var options = new WebApplicationFactoryClientOptions()
        {
            AllowAutoRedirect = false
        };
        var client = factory.CreateClient(options);
        var path = Oidc.Routes.BuildAuthorizeUrl(MemoryStorage.DemoRealm.Path)
            .AddQueryString("client_id", "demo_client")
            .AddQueryString("response_type", "code")
            .AddQueryString("response_mode", "query")
            .AddQueryString("scope", "openid profile")
            .AddQueryString("redirect_uri", "http://localhost:5000/callback")
            .AddQueryString("state", "state")
            .AddQueryString("code_challenge", "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")
            .AddQueryString("code_challenge_method", "S256");

        // Act
        var response = await client.GetAsync(path);

        // Assert
        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
    }

    [Fact]
    public async Task Get_Signed_Must_ReturnTheCode()
    {
        // Arrange
        var options = new WebApplicationFactoryClientOptions()
        {
            AllowAutoRedirect = false
        };
        var client = factory.CreateClient(options);
        await client.LoginAliceAsync();

        var path = Oidc.Routes.BuildAuthorizeUrl(MemoryStorage.DemoRealm.Path)
            .AddQueryString("client_id", "demo_client")
            .AddQueryString("response_type", "code")
            .AddQueryString("response_mode", "query")
            .AddQueryString("scope", "openid profile")
            .AddQueryString("redirect_uri", "http://localhost:5000/callback")
            .AddQueryString("state", "state")
            .AddQueryString("code_challenge", "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")
            .AddQueryString("code_challenge_method", "S256");

        // Act
        var response = await client.GetAsync(path);

        // Assert
        Assert.Equal(HttpStatusCode.Found, response.StatusCode);

        var location = response.Headers.Location?.ToString();
        Assert.NotNull(location);

        var query = location.Split('?')[1];
        var parameters = HttpUtility.ParseQueryString(query);
        var code = parameters["code"];

        Assert.NotNull(code);
    }

    [Fact]
    public async Task Get_WithoutParameters_Must_BadRequest()
    {
        // Arrange
        var client = factory.CreateClient();
        var path = Oidc.Routes.BuildAuthorizeUrl(MemoryStorage.DemoRealm.Path);

        // Act
        var response = await client.GetAsync(path);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Get_WithMinimumParameters_Must_RedirectToLoginPage()
    {
        // Arrange
        var storage = factory.Services.GetService<MemoryStorage>()!;
        storage.GetDemoRealmStore().Clients.TryAdd("client_with_secret", new RoyalIdentity.Models.Client()
        {
            Id = "client_with_secret",
            Name = "Client with Secret",
            RequireClientSecret = true,
            RequirePkce = false,
            AllowOfflineAccess = true,
            AllowedScopes = { "openid", "profile", "email" },
            AllowedResponseTypes = { "code" },
            RedirectUris = { "http://localhost:5000/**", "https://localhost:5001/**" },
            ClientSecrets = { new RoyalIdentity.Models.ClientSecret("secret") }
        });

        var options = new WebApplicationFactoryClientOptions()
        {
            AllowAutoRedirect = false
        };
        var client = factory.CreateClient(options);
        var path = Oidc.Routes.BuildAuthorizeUrl(MemoryStorage.DemoRealm.Path)
            .AddQueryString("client_id", "client_with_secret")
            .AddQueryString("redirect_uri", "http://localhost:5000/callback")
            .AddQueryString("response_type", "code")
            .AddQueryString("scope", "openid");

        // Act
        var response = await client.GetAsync(path);

        // Assert
        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
    }

    [Fact]
    public async Task Get_WithMinimumParameters_When_ClientRequirePkce_Must_BadRequest()
    {
        // Arrange
        var client = factory.CreateClient();
        var path = Oidc.Routes.BuildAuthorizeUrl(MemoryStorage.DemoRealm.Path)
            .AddQueryString("client_id", "demo_client")
            .AddQueryString("redirect_uri", "http://localhost:5000/callback")
            .AddQueryString("response_type", "code")
            .AddQueryString("scope", "openid");

        // Act
        var response = await client.GetAsync(path);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Get_WithoutScope_Must_BadRequest()
    {
        // Arrange
        var client = factory.CreateClient();
        var path = Oidc.Routes.BuildAuthorizeUrl(MemoryStorage.DemoRealm.Path)
            .AddQueryString("client_id", "demo_client")
            .AddQueryString("response_type", "code")
            .AddQueryString("response_mode", "query")
            .AddQueryString("redirect_uri", "http://localhost:5000/callback")
            .AddQueryString("state", "state")
            .AddQueryString("code_challenge", "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")
            .AddQueryString("code_challenge_method", "S256");

        // Act
        var response = await client.GetAsync(path);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Get_WithoutResponseType_Must_BadRequest()
    {
        // Arrange
        var client = factory.CreateClient();
        var path = Oidc.Routes.BuildAuthorizeUrl(MemoryStorage.DemoRealm.Path)
            .AddQueryString("client_id", "demo_client")
            .AddQueryString("response_mode", "query")
            .AddQueryString("scope", "openid profile")
            .AddQueryString("redirect_uri", "http://localhost:5000/callback")
            .AddQueryString("state", "state")
            .AddQueryString("code_challenge", "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")
            .AddQueryString("code_challenge_method", "S256");

        // Act
        var response = await client.GetAsync(path);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Get_WithoutRedirectUri_Must_BadRequest()
    {
        // Arrange
        var client = factory.CreateClient();
        var path = Oidc.Routes.BuildAuthorizeUrl(MemoryStorage.DemoRealm.Path)
            .AddQueryString("client_id", "demo_client")
            .AddQueryString("response_type", "code")
            .AddQueryString("response_mode", "query")
            .AddQueryString("scope", "openid profile")
            .AddQueryString("state", "state")
            .AddQueryString("code_challenge", "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")
            .AddQueryString("code_challenge_method", "S256");

        // Act
        var response = await client.GetAsync(path);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Get_WithoutClientId_Must_BadRequest()
    {
        // Arrange
        var client = factory.CreateClient();
        var path = Oidc.Routes.BuildAuthorizeUrl(MemoryStorage.DemoRealm.Path)
            .AddQueryString("response_type", "code")
            .AddQueryString("response_mode", "query")
            .AddQueryString("scope", "openid profile")
            .AddQueryString("redirect_uri", "http://localhost:5000/callback")
            .AddQueryString("state", "state")
            .AddQueryString("code_challenge", "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")
            .AddQueryString("code_challenge_method", "S256");

        // Act
        var response = await client.GetAsync(path);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Get_WithoutChallengeMethod_Must_BadRequest()
    {
        // Arrange
        var client = factory.CreateClient();
        var path = Oidc.Routes.BuildAuthorizeUrl(MemoryStorage.DemoRealm.Path)
            .AddQueryString("client_id", "demo_client")
            .AddQueryString("response_type", "code")
            .AddQueryString("response_mode", "query")
            .AddQueryString("scope", "openid profile")
            .AddQueryString("redirect_uri", "http://localhost:5000/callback")
            .AddQueryString("state", "state")
            .AddQueryString("code_challenge", "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA");

        // Act
        var response = await client.GetAsync(path);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Get_WithoutChallengeCode_Must_BadRequest()
    {
        // Arrange
        var client = factory.CreateClient();
        var path = Oidc.Routes.BuildAuthorizeUrl(MemoryStorage.DemoRealm.Path)
            .AddQueryString("client_id", "demo_client")
            .AddQueryString("response_type", "code")
            .AddQueryString("response_mode", "query")
            .AddQueryString("scope", "openid profile")
            .AddQueryString("redirect_uri", "http://localhost:5000/callback")
            .AddQueryString("state", "state")
            .AddQueryString("code_challenge_method", "S256");

        // Act
        var response = await client.GetAsync(path);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Get_WithoutPkce_Must_BadRequest()
    {
        // Arrange
        var client = factory.CreateClient();
        var path = Oidc.Routes.BuildAuthorizeUrl(MemoryStorage.DemoRealm.Path)
            .AddQueryString("client_id", "demo_client")
            .AddQueryString("response_type", "code")
            .AddQueryString("response_mode", "query")
            .AddQueryString("scope", "openid profile")
            .AddQueryString("redirect_uri", "http://localhost:5000/callback")
            .AddQueryString("state", "state");

        // Act
        var response = await client.GetAsync(path);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }


    [Fact]
    public async Task Get_WithoutResponseMode_Must_RedirectToLoginPage()
    {
        // Arrange
        var options = new WebApplicationFactoryClientOptions()
        {
            AllowAutoRedirect = false
        };
        var client = factory.CreateClient(options);
        var path = Oidc.Routes.BuildAuthorizeUrl(MemoryStorage.DemoRealm.Path)
            .AddQueryString("client_id", "demo_client")
            .AddQueryString("response_type", "code")
            .AddQueryString("scope", "openid profile")
            .AddQueryString("redirect_uri", "http://localhost:5000/callback")
            .AddQueryString("state", "state")
            .AddQueryString("code_challenge", "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")
            .AddQueryString("code_challenge_method", "S256");

        // Act
        var response = await client.GetAsync(path);

        // Assert
        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
    }

    [Fact]
    public async Task Get_WithoutState_Must_RedirectToLoginPage()
    {
        // Arrange
        var options = new WebApplicationFactoryClientOptions()
        {
            AllowAutoRedirect = false
        };
        var client = factory.CreateClient(options);
        var path = Oidc.Routes.BuildAuthorizeUrl(MemoryStorage.DemoRealm.Path)
            .AddQueryString("client_id", "demo_client")
            .AddQueryString("response_type", "code")
            .AddQueryString("response_mode", "query")
            .AddQueryString("scope", "openid profile")
            .AddQueryString("redirect_uri", "http://localhost:5000/callback")
            .AddQueryString("code_challenge", "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")
            .AddQueryString("code_challenge_method", "S256");

        // Act
        var response = await client.GetAsync(path);

        // Assert
        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
    }

    [Fact]
    public async Task Get_WithPlainChallengeMethod_When_PlainNotAllowed_Must_BadRequest()
    {
        // Arrange
        var options = new WebApplicationFactoryClientOptions()
        {
            AllowAutoRedirect = false
        };
        var client = factory.CreateClient(options);
        var path = Oidc.Routes.BuildAuthorizeUrl(MemoryStorage.DemoRealm.Path)
            .AddQueryString("client_id", "demo_client")
            .AddQueryString("response_type", "code")
            .AddQueryString("response_mode", "query")
            .AddQueryString("scope", "openid profile")
            .AddQueryString("redirect_uri", "http://localhost:5000/callback")
            .AddQueryString("code_challenge", "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")
            .AddQueryString("code_challenge_method", "Plain");

        // Act
        var response = await client.GetAsync(path);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
