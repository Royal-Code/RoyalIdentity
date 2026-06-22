// Ignore Spelling: Pkce

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using RoyalIdentity.Extensions;
using RoyalIdentity.Models;
using RoyalIdentity.Models.Scopes;
using RoyalIdentity.Utils;
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
            AllowedIdentityScopes = { "openid", "profile", "email" },
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
            .AddQueryString("code_challenge_method", Oidc.CodeChallenge.Methods.Plain);

        // Act
        var response = await client.GetAsync(path);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Get_WithImplicitResourceIndicatorAndNoApiScope_ShouldReturnAccessToken()
    {
        var storage = factory.Services.GetRequiredService<MemoryStorage>();
        var suffix = CryptoRandom.CreateUniqueId(4, CryptoRandom.OutputFormat.Hex);
        var clientId = $"implicit-resource-client-{suffix}";
        storage.GetDemoRealmStore().Clients[clientId] = new Client
        {
            Realm = MemoryStorage.DemoRealm,
            Id = clientId,
            Name = "Implicit Resource Client",
            RequireClientSecret = false,
            RequirePkce = false,
            AllowedGrantTypes = ["implicit"],
            AllowedIdentityScopes = { "openid" },
            AllowedResourceServers = { "apiserver" },
            AllowedResponseTypes = { "token" },
            RedirectUris = { "http://localhost:5000/**" }
        };

        var options = new WebApplicationFactoryClientOptions()
        {
            AllowAutoRedirect = false
        };
        var client = factory.CreateClient(options);
        await client.LoginAliceAsync();

        var path = Oidc.Routes.BuildAuthorizeUrl(MemoryStorage.DemoRealm.Path)
            .AddQueryString("client_id", clientId)
            .AddQueryString("response_type", "token")
            .AddQueryString("response_mode", "form_post")
            .AddQueryString("scope", "openid")
            .AddQueryString("resource", "https://api.demo.local/apiserver")
            .AddQueryString("nonce", "implicit-resource-nonce")
            .AddQueryString("redirect_uri", "http://localhost:5000/callback");

        var response = await client.GetAsync(path);
        var body = await response.Content.ReadAsStringAsync();

        Assert.True(response.StatusCode == HttpStatusCode.OK, body);
        Assert.Contains("access_token", body);
        Assert.DoesNotContain("invalid_scope", body);
    }

    [Fact]
    public async Task Get_WithIdTokenOnlyResponseTypeAndResourceIndicator_ShouldReturnInvalidScope()
    {
        // ADR-012 (l.91): id_token-only requests are restricted to identity scopes. A resource indicator
        // pulls in a resource server, which is not allowed for response_type=id_token only.
        var storage = factory.Services.GetRequiredService<MemoryStorage>();
        var suffix = CryptoRandom.CreateUniqueId(4, CryptoRandom.OutputFormat.Hex);
        var clientId = $"id-token-resource-client-{suffix}";
        storage.GetDemoRealmStore().Clients[clientId] = new Client
        {
            Realm = MemoryStorage.DemoRealm,
            Id = clientId,
            Name = "Id Token Resource Client",
            RequireClientSecret = false,
            RequirePkce = false,
            AllowedGrantTypes = ["implicit"],
            AllowedIdentityScopes = { "openid" },
            AllowedResourceServers = { "apiserver" },
            AllowedResponseTypes = { "id_token" },
            RedirectUris = { "http://localhost:5000/**" }
        };

        var options = new WebApplicationFactoryClientOptions() { AllowAutoRedirect = false };
        var client = factory.CreateClient(options);
        await client.LoginAliceAsync();

        var path = Oidc.Routes.BuildAuthorizeUrl(MemoryStorage.DemoRealm.Path)
            .AddQueryString("client_id", clientId)
            .AddQueryString("response_type", "id_token")
            .AddQueryString("response_mode", "form_post")
            .AddQueryString("scope", "openid")
            .AddQueryString("resource", "https://api.demo.local/apiserver")
            .AddQueryString("nonce", "id-token-resource-nonce")
            .AddQueryString("redirect_uri", "http://localhost:5000/callback");

        var response = await client.GetAsync(path);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("invalid_scope", body);
    }

    [Fact]
    public async Task Get_WhenScopesRequireIncompatibleSigningAlgorithms_ShouldReturnInvalidRequest()
    {
        // ADR-010/ADR-012: two requested resource servers with mutually incompatible signing-algorithm
        // filters cannot agree on an algorithm; the authorize request is rejected with invalid_request
        // (caught at ResourcesValidator, before login).
        var storage = factory.Services.GetRequiredService<MemoryStorage>();
        var store = storage.GetDemoRealmStore();
        var suffix = CryptoRandom.CreateUniqueId(4, CryptoRandom.OutputFormat.Hex);

        var firstServer = $"signing-a-{suffix}";
        var firstScope = $"{firstServer}:read";
        store.ResourceServers[firstServer] = new ResourceServer(
            ScopeVisibility.Public, firstServer, "Signing A", "Signing A")
        {
            Scopes = [new Scope(ScopeVisibility.Public, firstScope, "read", "read")],
            AllowedAccessTokenSigningAlgorithms = [SecurityAlgorithms.RsaSha256]
        };

        var secondServer = $"signing-b-{suffix}";
        var secondScope = $"{secondServer}:read";
        store.ResourceServers[secondServer] = new ResourceServer(
            ScopeVisibility.Public, secondServer, "Signing B", "Signing B")
        {
            Scopes = [new Scope(ScopeVisibility.Public, secondScope, "read", "read")],
            AllowedAccessTokenSigningAlgorithms = [SecurityAlgorithms.EcdsaSha256]
        };

        var clientId = $"incompatible-signing-client-{suffix}";
        store.Clients[clientId] = new Client
        {
            Realm = MemoryStorage.DemoRealm,
            Id = clientId,
            Name = "Incompatible Signing Client",
            RequireClientSecret = false,
            RequirePkce = false,
            AllowedGrantTypes = ["authorization_code"],
            AllowedResourceServers = { firstServer, secondServer },
            AllowedResponseTypes = { "code" },
            RedirectUris = { "http://localhost:5000/**" }
        };

        var client = factory.CreateClient();
        var path = Oidc.Routes.BuildAuthorizeUrl(MemoryStorage.DemoRealm.Path)
            .AddQueryString("client_id", clientId)
            .AddQueryString("response_type", "code")
            .AddQueryString("response_mode", "query")
            .AddQueryString("scope", $"{firstScope} {secondScope}")
            .AddQueryString("redirect_uri", "http://localhost:5000/callback");

        var response = await client.GetAsync(path);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("invalid_request", body);
        Assert.Contains("Signing algorithms requirements", body);
    }
}
