// Ignore Spelling: Pkce

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.IdentityModel.Tokens;
using RoyalIdentity.Extensions;
using RoyalIdentity.Models;
using RoyalIdentity.Models.Scopes;
using RoyalIdentity.Utils;
using System.Net;
using System.Web;
using Tests.Integration.Prepare;

namespace Tests.Integration.Endpoints;

public class CodeAuthorizeTests : IClassFixture<PersistentStorageAppFactory>
{
    private readonly PersistentStorageAppFactory factory;

    public CodeAuthorizeTests(PersistentStorageAppFactory factory)
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
        var path = Oidc.Routes.BuildAuthorizeUrl(factory.Handles.Demo.Path)
            .AddQueryString("client_id", factory.Handles.DemoClient.ClientId)
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
        await client.LoginAsync(factory.Handles.Demo, factory.Handles.Alice);

        var path = Oidc.Routes.BuildAuthorizeUrl(factory.Handles.Demo.Path)
            .AddQueryString("client_id", factory.Handles.DemoClient.ClientId)
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
        var path = Oidc.Routes.BuildAuthorizeUrl(factory.Handles.Demo.Path);

        // Act
        var response = await client.GetAsync(path);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Get_WithMinimumParameters_Must_RedirectToLoginPage()
    {
        // Arrange
        const string clientId = "client_with_secret";
        await factory.SaveClientAsync(
            factory.Handles.Demo,
            clientId,
            configured =>
            {
                configured.Name = "Client with Secret";
                configured.RequireClientSecret = true;
                configured.RequirePkce = false;
                configured.AllowOfflineAccess = true;
                configured.AllowedIdentityScopes.UnionWith(["openid", "profile", "email"]);
                configured.AllowedResponseTypes.Add("code");
                configured.RedirectUris.UnionWith(
                    ["http://localhost:5000/**", "https://localhost:5001/**"]);
                configured.Secrets.Add(new ClientSecret("secret"));
            });

        var options = new WebApplicationFactoryClientOptions()
        {
            AllowAutoRedirect = false
        };
        var client = factory.CreateClient(options);
        var path = Oidc.Routes.BuildAuthorizeUrl(factory.Handles.Demo.Path)
            .AddQueryString("client_id", clientId)
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
        var path = Oidc.Routes.BuildAuthorizeUrl(factory.Handles.Demo.Path)
            .AddQueryString("client_id", factory.Handles.DemoClient.ClientId)
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
        var path = Oidc.Routes.BuildAuthorizeUrl(factory.Handles.Demo.Path)
            .AddQueryString("client_id", factory.Handles.DemoClient.ClientId)
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
        var path = Oidc.Routes.BuildAuthorizeUrl(factory.Handles.Demo.Path)
            .AddQueryString("client_id", factory.Handles.DemoClient.ClientId)
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
        var path = Oidc.Routes.BuildAuthorizeUrl(factory.Handles.Demo.Path)
            .AddQueryString("client_id", factory.Handles.DemoClient.ClientId)
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
        var path = Oidc.Routes.BuildAuthorizeUrl(factory.Handles.Demo.Path)
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
        var path = Oidc.Routes.BuildAuthorizeUrl(factory.Handles.Demo.Path)
            .AddQueryString("client_id", factory.Handles.DemoClient.ClientId)
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
        var path = Oidc.Routes.BuildAuthorizeUrl(factory.Handles.Demo.Path)
            .AddQueryString("client_id", factory.Handles.DemoClient.ClientId)
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
        var path = Oidc.Routes.BuildAuthorizeUrl(factory.Handles.Demo.Path)
            .AddQueryString("client_id", factory.Handles.DemoClient.ClientId)
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
        var path = Oidc.Routes.BuildAuthorizeUrl(factory.Handles.Demo.Path)
            .AddQueryString("client_id", factory.Handles.DemoClient.ClientId)
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
        var path = Oidc.Routes.BuildAuthorizeUrl(factory.Handles.Demo.Path)
            .AddQueryString("client_id", factory.Handles.DemoClient.ClientId)
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
        var path = Oidc.Routes.BuildAuthorizeUrl(factory.Handles.Demo.Path)
            .AddQueryString("client_id", factory.Handles.DemoClient.ClientId)
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
        var suffix = CryptoRandom.CreateUniqueId(4, OutputFormat.Hex);
        var clientId = $"implicit-resource-client-{suffix}";
        await factory.SaveClientAsync(
            factory.Handles.Demo,
            clientId,
            configured =>
            {
                configured.Name = "Implicit Resource Client";
                configured.RequireClientSecret = false;
                configured.RequirePkce = false;
                configured.AllowedGrantTypes.Clear();
                configured.AllowedGrantTypes.Add("implicit");
                configured.AllowedIdentityScopes.Add("openid");
                configured.AllowedResourceServers.Add("apiserver");
                configured.AllowedResponseTypes.Clear();
                configured.AllowedResponseTypes.Add("token");
                configured.RedirectUris.Add("http://localhost:5000/**");
            });

        var options = new WebApplicationFactoryClientOptions()
        {
            AllowAutoRedirect = false
        };
        var client = factory.CreateClient(options);
        await client.LoginAsync(factory.Handles.Demo, factory.Handles.Alice);

        var path = Oidc.Routes.BuildAuthorizeUrl(factory.Handles.Demo.Path)
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
        var suffix = CryptoRandom.CreateUniqueId(4, OutputFormat.Hex);
        var clientId = $"id-token-resource-client-{suffix}";
        await factory.SaveClientAsync(
            factory.Handles.Demo,
            clientId,
            configured =>
            {
                configured.Name = "Id Token Resource Client";
                configured.RequireClientSecret = false;
                configured.RequirePkce = false;
                configured.AllowedGrantTypes.Clear();
                configured.AllowedGrantTypes.Add("implicit");
                configured.AllowedIdentityScopes.Add("openid");
                configured.AllowedResourceServers.Add("apiserver");
                configured.AllowedResponseTypes.Clear();
                configured.AllowedResponseTypes.Add("id_token");
                configured.RedirectUris.Add("http://localhost:5000/**");
            });

        var options = new WebApplicationFactoryClientOptions() { AllowAutoRedirect = false };
        var client = factory.CreateClient(options);
        await client.LoginAsync(factory.Handles.Demo, factory.Handles.Alice);

        var path = Oidc.Routes.BuildAuthorizeUrl(factory.Handles.Demo.Path)
            .AddQueryString("client_id", clientId)
            .AddQueryString("response_type", "id_token")
            .AddQueryString("response_mode", "form_post")
            .AddQueryString("scope", "openid")
            .AddQueryString("resource", "https://api.demo.local/apiserver")
            .AddQueryString("nonce", "id-token-resource-nonce")
            .AddQueryString("redirect_uri", "http://localhost:5000/callback");

        var response = await client.GetAsync(path);

        await response.AssertErrorAsync(Oidc.Authorize.Errors.InvalidScope);
    }

    [Fact]
    public async Task Get_WhenScopesRequireIncompatibleSigningAlgorithms_ShouldReturnInvalidRequest()
    {
        // ADR-010/ADR-012: two requested resource servers with mutually incompatible signing-algorithm
        // filters cannot agree on an algorithm; the authorize request is rejected with invalid_request
        // (caught at ResourcesValidator, before login).
        var suffix = CryptoRandom.CreateUniqueId(4, OutputFormat.Hex);

        var firstServer = $"signing-a-{suffix}";
        var firstScope = $"{firstServer}:read";
        factory.Resources.SetResourceServer(
            factory.Handles.Demo.Id,
            new ResourceServer(ScopeVisibility.Public, firstServer, "Signing A", "Signing A")
            {
                Scopes = [new Scope(ScopeVisibility.Public, firstScope, "read", "read")],
                AllowedAccessTokenSigningAlgorithms = [SecurityAlgorithms.RsaSha256]
            });

        var secondServer = $"signing-b-{suffix}";
        var secondScope = $"{secondServer}:read";
        factory.Resources.SetResourceServer(
            factory.Handles.Demo.Id,
            new ResourceServer(ScopeVisibility.Public, secondServer, "Signing B", "Signing B")
            {
                Scopes = [new Scope(ScopeVisibility.Public, secondScope, "read", "read")],
                AllowedAccessTokenSigningAlgorithms = [SecurityAlgorithms.EcdsaSha256]
            });

        var clientId = $"incompatible-signing-client-{suffix}";
        await factory.SaveClientAsync(
            factory.Handles.Demo,
            clientId,
            configured =>
            {
                configured.Name = "Incompatible Signing Client";
                configured.RequireClientSecret = false;
                configured.RequirePkce = false;
                configured.AllowedGrantTypes.Add("authorization_code");
                configured.AllowedResourceServers.UnionWith([firstServer, secondServer]);
                configured.AllowedResponseTypes.Add("code");
                configured.RedirectUris.Add("http://localhost:5000/**");
            });

        var client = factory.CreateClient();
        var path = Oidc.Routes.BuildAuthorizeUrl(factory.Handles.Demo.Path)
            .AddQueryString("client_id", clientId)
            .AddQueryString("response_type", "code")
            .AddQueryString("response_mode", "query")
            .AddQueryString("scope", $"{firstScope} {secondScope}")
            .AddQueryString("redirect_uri", "http://localhost:5000/callback");

        var response = await client.GetAsync(path);

        // DF20: correcting the shared ResourcesValidator moved invalid_scope/invalid_target into the error
        // field, and this branch must keep the classification it already had.
        var error = await response.AssertErrorAsync(Oidc.Authorize.Errors.InvalidRequest);
        Assert.Contains("Signing algorithms requirements", error.Description ?? string.Empty);
    }

    // -----------------------------------------------------------------------------------------------------
    // DF20 regression: the shared ResourcesValidator/ResourcesDecorator used to bury the real code inside
    // error_description and answer invalid_request. The authorize endpoint now names the code in the error
    // field. The transport (JSON here, instead of a redirect carrying error/state) stays deferred.
    // -----------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Get_WithoutResponseType_Must_AnswerUnsupportedResponseTypeInTheErrorField()
    {
        var client = factory.CreateClient();
        var path = Oidc.Routes.BuildAuthorizeUrl(factory.Handles.Demo.Path)
            .AddQueryString("client_id", factory.Handles.DemoClient.ClientId)
            .AddQueryString("response_mode", "query")
            .AddQueryString("scope", "openid profile")
            .AddQueryString("redirect_uri", "http://localhost:5000/callback")
            .AddQueryString("state", "state")
            .AddQueryString("code_challenge", "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")
            .AddQueryString("code_challenge_method", "S256");

        var response = await client.GetAsync(path);

        var error = await response.AssertErrorAsync(Oidc.Authorize.Errors.UnsupportedResponseType);
        Assert.DoesNotContain(Oidc.Authorize.Errors.UnsupportedResponseType, error.Description ?? string.Empty);
    }

    [Fact]
    public async Task Get_WithUnsupportedResponseMode_Must_AnswerUnsupportedResponseModeInTheErrorField()
    {
        var client = factory.CreateClient();
        var path = Oidc.Routes.BuildAuthorizeUrl(factory.Handles.Demo.Path)
            .AddQueryString("client_id", factory.Handles.DemoClient.ClientId)
            .AddQueryString("response_type", "code")
            .AddQueryString("response_mode", "no-such-response-mode")
            .AddQueryString("scope", "openid profile")
            .AddQueryString("redirect_uri", "http://localhost:5000/callback")
            .AddQueryString("state", "state")
            .AddQueryString("code_challenge", "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")
            .AddQueryString("code_challenge_method", "S256");

        var response = await client.GetAsync(path);

        // This one used to answer invalid_request with the real code as the whole description, because the
        // constant was the single argument of InvalidRequest(...).
        var error = await response.AssertErrorAsync(Oidc.Authorize.Errors.UnsupportedResponseMode);
        Assert.DoesNotContain(Oidc.Authorize.Errors.UnsupportedResponseMode, error.Description ?? string.Empty);
    }

    [Fact]
    public async Task Get_WithUnknownClient_Must_AnswerUnauthorizedClientInTheErrorField()
    {
        var client = factory.CreateClient();
        var path = Oidc.Routes.BuildAuthorizeUrl(factory.Handles.Demo.Path)
            .AddQueryString("client_id", $"no-such-client-{CryptoRandom.CreateUniqueId(6)}")
            .AddQueryString("response_type", "code")
            .AddQueryString("response_mode", "query")
            .AddQueryString("scope", "openid profile")
            .AddQueryString("redirect_uri", "http://localhost:5000/callback")
            .AddQueryString("state", "state")
            .AddQueryString("code_challenge", "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")
            .AddQueryString("code_challenge_method", "S256");

        var response = await client.GetAsync(path);

        var error = await response.AssertErrorAsync(Oidc.Authorize.Errors.UnauthorizedClient);
        Assert.DoesNotContain(Oidc.Authorize.Errors.UnauthorizedClient, error.Description ?? string.Empty);
    }

    [Fact]
    public async Task Get_WithoutClientId_Must_AnswerInvalidRequestInTheErrorField()
    {
        // The counterpart of the case above: a missing client_id is a malformed request, not an unauthorized
        // client, and the two must not have converged while the callers were migrated.
        var client = factory.CreateClient();
        var path = Oidc.Routes.BuildAuthorizeUrl(factory.Handles.Demo.Path)
            .AddQueryString("response_type", "code")
            .AddQueryString("response_mode", "query")
            .AddQueryString("scope", "openid profile")
            .AddQueryString("redirect_uri", "http://localhost:5000/callback")
            .AddQueryString("state", "state");

        var response = await client.GetAsync(path);

        await response.AssertErrorAsync(Oidc.Authorize.Errors.InvalidRequest);
    }

    [Fact]
    public async Task Get_WithUnknownScope_Must_AnswerInvalidScopeInTheErrorField()
    {
        var client = factory.CreateClient();
        var path = Oidc.Routes.BuildAuthorizeUrl(factory.Handles.Demo.Path)
            .AddQueryString("client_id", factory.Handles.DemoClient.ClientId)
            .AddQueryString("response_type", "code")
            .AddQueryString("response_mode", "query")
            .AddQueryString("scope", $"openid no-such-scope-{CryptoRandom.CreateUniqueId(4, OutputFormat.Hex)}")
            .AddQueryString("redirect_uri", "http://localhost:5000/callback")
            .AddQueryString("state", "state")
            .AddQueryString("code_challenge", "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")
            .AddQueryString("code_challenge_method", "S256");

        var response = await client.GetAsync(path);

        var error = await response.AssertErrorAsync(Oidc.Authorize.Errors.InvalidScope);
        Assert.DoesNotContain(Oidc.Authorize.Errors.InvalidScope, error.Description ?? string.Empty);
    }

    [Fact]
    public async Task Get_WithUnknownResourceIndicator_Must_AnswerInvalidTargetInTheErrorField()
    {
        var client = factory.CreateClient();
        var path = Oidc.Routes.BuildAuthorizeUrl(factory.Handles.Demo.Path)
            .AddQueryString("client_id", factory.Handles.DemoClient.ClientId)
            .AddQueryString("response_type", "code")
            .AddQueryString("response_mode", "query")
            .AddQueryString("scope", "openid profile")
            .AddQueryString("resource", "https://unknown.example/api")
            .AddQueryString("redirect_uri", "http://localhost:5000/callback")
            .AddQueryString("state", "state")
            .AddQueryString("code_challenge", "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")
            .AddQueryString("code_challenge_method", "S256");

        var response = await client.GetAsync(path);

        var error = await response.AssertErrorAsync(Oidc.Authorize.Errors.InvalidTarget);
        Assert.DoesNotContain(Oidc.Authorize.Errors.InvalidTarget, error.Description ?? string.Empty);
    }
}
