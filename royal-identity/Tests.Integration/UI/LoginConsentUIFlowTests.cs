using HtmlAgilityPack;
using Microsoft.Extensions.DependencyInjection;
using RoyalIdentity.Extensions;
using RoyalIdentity.Models;
using RoyalIdentity.Responses.HttpResults;
using RoyalIdentity.Utils;
using System.Net;
using System.Text;
using System.Text.Json;
using Tests.Integration.Prepare;

namespace Tests.Integration.UI;

public class LoginConsentUIFlowTests : IClassFixture<AppFactory>
{
    private readonly AppFactory factory;

    public LoginConsentUIFlowTests(AppFactory factory)
    {
        this.factory = factory;
    }

    [Fact]
    public async Task Login_WhenValid_MustRedirectToRequestedOrigin()
    {
        // 1 - Login
        // Arrange 1

        var client = factory.CreateClient();

        var codeVerifier = CryptoRandom.CreateUniqueId();
        var codeVerifierBytes = Encoding.ASCII.GetBytes(codeVerifier);
        var codeChallenge = Base64Url.Encode(codeVerifierBytes.Sha256());

        var path = Oidc.Routes.BuildAuthorizeUrl(MemoryStorage.DemoRealm.Path)
            .AddQueryString("client_id", "demo_client")
            .AddQueryString("response_type", "code")
            .AddQueryString("response_mode", "query")
            .AddQueryString("scope", "openid profile email offline_access")
            .AddQueryString("redirect_uri", $"{client.BaseAddress}callback")
            .AddQueryString("state", "state")
            .AddQueryString("code_challenge", codeChallenge)
            .AddQueryString("code_challenge_method", "S256");

        // will redirect to login page
        var loginPage = await client.GetAsync(path);
        var content = await loginPage.Content.ReadAsStringAsync();
        var doc = new HtmlDocument();
        doc.LoadHtml(content);
        var form = doc.DocumentNode.SelectSingleNode("//form");
        var formAction = new FormAction(client, form)
            .SetValue("Input.Username", "alice")
            .SetValue("Input.Password", "alice");

        // Act 1
        // after login, will redirect to resource server callback
        var response = await formAction.SubmitAsync();

        // the redirect_uri will return the query string as json
        var json = await response.Content.ReadAsStringAsync();
        var callbackData = JsonSerializer.Deserialize<Dictionary<string, string>>(json);

        // Assert 1
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(content);
        Assert.NotNull(callbackData);

        //***********************************************

        // 2 - Generate Token

        // Arrange 2
        var tokenContent = new FormUrlEncodedContent(
                new Dictionary<string, string>
                {
                    ["grant_type"] = "authorization_code",
                    ["code"] = callbackData["code"],
                    ["client_id"] = "demo_client",
                    ["redirect_uri"] = $"{client.BaseAddress}callback",
                    ["code_verifier"] = codeVerifier
                });

        var tokenUrl = Oidc.Routes.BuildTokenUrl(MemoryStorage.DemoRealm.Path);

        // Act 2
        var tokenResponse = await client.PostAsync(tokenUrl, tokenContent);
        var tokenJson = await tokenResponse.Content.ReadAsStringAsync();

        // Assert 2
        Assert.Equal(HttpStatusCode.OK, tokenResponse.StatusCode);
        Assert.NotNull(tokenJson);

        var tokenEndpointParameters = JsonSerializer.Deserialize<TokenEndpointParameters>(tokenJson);
        // must contains: access_token, token_type, expires_in, refresh_token, id_token, scope
        Assert.NotNull(tokenEndpointParameters);
        Assert.NotNull(tokenEndpointParameters.AccessToken);
        Assert.NotNull(tokenEndpointParameters.TokenType);
        Assert.NotEqual(0, tokenEndpointParameters.ExpiresIn);
        Assert.NotNull(tokenEndpointParameters.RefreshToken);
        Assert.NotNull(tokenEndpointParameters.IdentityToken);
        Assert.NotNull(tokenEndpointParameters.Scope);
    }

    [Fact]
    public async Task Login_WithPlainPkce_WhenClientAllowsPlain_MustGenerateToken()
    {
        var client = factory.CreateClient();
        var storage = factory.Services.GetRequiredService<MemoryStorage>();
        var suffix = CryptoRandom.CreateUniqueId(4, OutputFormat.Hex);
        var clientId = $"plain-pkce-client-{suffix}";
        var redirectUri = $"{client.BaseAddress}callback";

        storage.GetDemoRealmStore().Clients[clientId] = new Client
        {
            Realm = MemoryStorage.DemoRealm,
            Id = clientId,
            Name = "Plain PKCE Client",
            RequireClientSecret = false,
            RequirePkce = true,
            AllowPlainTextPkce = true,
            AllowedIdentityScopes = { "openid", "profile" },
            AllowedResponseTypes = { "code" },
            RedirectUris = { redirectUri }
        };

        var codeVerifier = CryptoRandom.CreateUniqueId();
        var path = Oidc.Routes.BuildAuthorizeUrl(MemoryStorage.DemoRealm.Path)
            .AddQueryString("client_id", clientId)
            .AddQueryString("response_type", "code")
            .AddQueryString("response_mode", "query")
            .AddQueryString("scope", "openid profile")
            .AddQueryString("redirect_uri", redirectUri)
            .AddQueryString("state", "state")
            .AddQueryString("code_challenge", codeVerifier)
            .AddQueryString("code_challenge_method", Oidc.CodeChallenge.Methods.Plain);

        var loginPage = await client.GetAsync(path);
        var content = await loginPage.Content.ReadAsStringAsync();
        var doc = new HtmlDocument();
        doc.LoadHtml(content);
        var form = doc.DocumentNode.SelectSingleNode("//form");
        var formAction = new FormAction(client, form)
            .SetValue("Input.Username", "alice")
            .SetValue("Input.Password", "alice");

        var response = await formAction.SubmitAsync();
        var json = await response.Content.ReadAsStringAsync();
        var callbackData = JsonSerializer.Deserialize<Dictionary<string, string>>(json);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(callbackData);
        Assert.Contains("code", callbackData.Keys);

        var tokenContent = new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["grant_type"] = "authorization_code",
                ["code"] = callbackData["code"],
                ["client_id"] = clientId,
                ["redirect_uri"] = redirectUri,
                ["code_verifier"] = codeVerifier
            });

        var tokenUrl = Oidc.Routes.BuildTokenUrl(MemoryStorage.DemoRealm.Path);
        var tokenResponse = await client.PostAsync(tokenUrl, tokenContent);
        var tokenJson = await tokenResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, tokenResponse.StatusCode);

        var tokenEndpointParameters = JsonSerializer.Deserialize<TokenEndpointParameters>(tokenJson);
        Assert.NotNull(tokenEndpointParameters);
        Assert.NotNull(tokenEndpointParameters.AccessToken);
        Assert.NotNull(tokenEndpointParameters.IdentityToken);
    }

    [Fact]
    public async Task Login_WhenValid_MustShowConsent_WhenClientRequires_ThenRedirectToRequestedOrigin()
    {
        // 1 - Login and Consent
        // Arrange 1

        var client = factory.CreateClient();

        var codeVerifier = CryptoRandom.CreateUniqueId();
        var codeVerifierBytes = Encoding.ASCII.GetBytes(codeVerifier);
        var codeChallenge = Base64Url.Encode(codeVerifierBytes.Sha256());

        var path = Oidc.Routes.BuildAuthorizeUrl(MemoryStorage.DemoRealm.Path)
            .AddQueryString("client_id", "demo_consent_client")
            .AddQueryString("response_type", "code")
            .AddQueryString("response_mode", "query")
            .AddQueryString("scope", "openid profile email api api:read api:write offline_access")
            .AddQueryString("redirect_uri", $"{client.BaseAddress}callback")
            .AddQueryString("state", "state")
            .AddQueryString("code_challenge", codeChallenge)
            .AddQueryString("code_challenge_method", "S256");

        // will redirect to login page
        var loginPage = await client.GetAsync(path);
        var content = await loginPage.Content.ReadAsStringAsync();
        var doc = new HtmlDocument();
        doc.LoadHtml(content);
        var form = doc.DocumentNode.SelectSingleNode("//form");
        var formAction = new FormAction(client, form)
            .SetValue("Input.Username", "alice")
            .SetValue("Input.Password", "alice");

        // Act 1
        // after login, will redirect to consent page
        var response = await formAction.SubmitAsync();
        content = await response.Content.ReadAsStringAsync();
        doc.LoadHtml(content);
        form = doc.DocumentNode.SelectSingleNode("//form");
        formAction = new FormAction(client, form);
        
        // after consent, will return consented page
        response = await formAction.SubmitAsync();
        content = await response.Content.ReadAsStringAsync();
                
        // Assert 1
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(content);

        //***********************************************

        // 2 - Generate Code

        // Arrange 2
        doc = new HtmlDocument();
        doc.LoadHtml(content);

        // get div with id="consent-redirect"
        var consentRedirect = doc.DocumentNode.SelectSingleNode("//div[@id='consent-redirect']");
        var returnUrl = consentRedirect.GetAttributeValue("data-returnUrl", "");

        // Act 2

        // execute the authorize callback
        // the response will be a redirect to the redirect_uri endpoint
        response = await client.GetAsync(returnUrl);

        // the redirect_uri will return the query string as json
        var json = await response.Content.ReadAsStringAsync();
        var callbackData = JsonSerializer.Deserialize<Dictionary<string, string>>(json);

        // Assert 2
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(callbackData);

        // must contains: code, state, scope and session_state
        Assert.Contains("code", callbackData.Keys);
        Assert.Contains("state", callbackData.Keys);
        Assert.Contains("scope", callbackData.Keys);
        Assert.Contains("session_state", callbackData.Keys);

        //***********************************************

        // 3 - Generate Token

        // Arrange 3
        var tokenContent = new FormUrlEncodedContent(
                new Dictionary<string, string>
                {
                    ["grant_type"] = "authorization_code",
                    ["code"] = callbackData["code"],
                    ["client_id"] = "demo_consent_client",
                    ["redirect_uri"] = $"{client.BaseAddress}callback",
                    ["code_verifier"] = codeVerifier
                });

        var tokenUrl = Oidc.Routes.BuildTokenUrl(MemoryStorage.DemoRealm.Path);

        // Act 3
        var tokenResponse = await client.PostAsync(tokenUrl, tokenContent);
        var tokenJson = await tokenResponse.Content.ReadAsStringAsync();

        // Assert 3
        Assert.Equal(HttpStatusCode.OK, tokenResponse.StatusCode);
        Assert.NotNull(tokenJson);

        var tokenEndpointParameters = JsonSerializer.Deserialize<TokenEndpointParameters>(tokenJson);
        // must contains: access_token, token_type, expires_in, refresh_token, id_token, scope
        Assert.NotNull(tokenEndpointParameters);
        Assert.NotNull(tokenEndpointParameters.AccessToken);
        Assert.NotNull(tokenEndpointParameters.TokenType);
        Assert.NotEqual(0, tokenEndpointParameters.ExpiresIn);
        Assert.NotNull(tokenEndpointParameters.RefreshToken);
        Assert.NotNull(tokenEndpointParameters.IdentityToken);
        Assert.NotNull(tokenEndpointParameters.Scope);
    }

    [Fact]
    public async Task Consent_WhenResourceRequested_MustShowProtectedResourceGroupedByResourceServer()
    {
        // Fase 7 acceptance: with a resource indicator (RFC 8707) requested, the consent screen must
        // group scopes by resource server and show the protected resource (audience-only) for that server.
        // Arrange

        var client = factory.CreateClient();

        var codeVerifier = CryptoRandom.CreateUniqueId();
        var codeVerifierBytes = Encoding.ASCII.GetBytes(codeVerifier);
        var codeChallenge = Base64Url.Encode(codeVerifierBytes.Sha256());

        var path = Oidc.Routes.BuildAuthorizeUrl(MemoryStorage.DemoRealm.Path)
            .AddQueryString("client_id", "demo_consent_client")
            .AddQueryString("response_type", "code")
            .AddQueryString("response_mode", "query")
            .AddQueryString("scope", "openid profile email api api:read api:write offline_access")
            .AddQueryString("resource", "https://api.demo.local/apiserver")
            .AddQueryString("redirect_uri", $"{client.BaseAddress}callback")
            .AddQueryString("state", "state")
            .AddQueryString("code_challenge", codeChallenge)
            .AddQueryString("code_challenge_method", "S256");

        // will redirect to login page
        var loginPage = await client.GetAsync(path);
        var content = await loginPage.Content.ReadAsStringAsync();
        var doc = new HtmlDocument();
        doc.LoadHtml(content);
        var form = doc.DocumentNode.SelectSingleNode("//form");
        // 'bob' has no persisted consent (the denial test never stores it), so this always reaches consent.
        // This test does not submit the consent, so it persists nothing and stays isolated.
        var formAction = new FormAction(client, form)
            .SetValue("Input.Username", "bob")
            .SetValue("Input.Password", "bob");

        // Act
        // after login, the response is the consent screen
        var response = await formAction.SubmitAsync();
        content = await response.Content.ReadAsStringAsync();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        // the resource server group is shown with its scopes
        Assert.Contains("API Server", content);
        Assert.Contains("API read", content);
        Assert.Contains("API write", content);
        // the protected resource (audience-only) is shown by its URI
        Assert.Contains("https://api.demo.local/apiserver", content);
        // and the consent form is present (the user can still grant/deny)
        Assert.Contains("Yes, Allow", content);
    }

    [Fact]
    public async Task Login_WhenUserDeniesConsent_MustRedirectToClient_WithAccessDenied()
    {
        // Arrange

        var client = factory.CreateClient();

        var codeVerifier = CryptoRandom.CreateUniqueId();
        var codeVerifierBytes = Encoding.ASCII.GetBytes(codeVerifier);
        var codeChallenge = Base64Url.Encode(codeVerifierBytes.Sha256());

        var path = Oidc.Routes.BuildAuthorizeUrl(MemoryStorage.DemoRealm.Path)
            .AddQueryString("client_id", "demo_consent_client")
            .AddQueryString("response_type", "code")
            .AddQueryString("response_mode", "query")
            .AddQueryString("scope", "openid profile email api api:read api:write offline_access")
            .AddQueryString("redirect_uri", $"{client.BaseAddress}callback")
            .AddQueryString("state", "state")
            .AddQueryString("code_challenge", codeChallenge)
            .AddQueryString("code_challenge_method", "S256");

        // will redirect to login page
        var loginPage = await client.GetAsync(path);
        var content = await loginPage.Content.ReadAsStringAsync();
        var doc = new HtmlDocument();
        doc.LoadHtml(content);
        var form = doc.DocumentNode.SelectSingleNode("//form");
        // Use 'bob' (not 'alice') so this test does not depend on consent state another test may have
        // persisted for the same (subject, client). The denial path never stores consent, so 'bob'
        // always reaches the consent screen here.
        var formAction = new FormAction(client, form)
            .SetValue("Input.Username", "bob")
            .SetValue("Input.Password", "bob");

        // after login, will redirect to consent page
        var response = await formAction.SubmitAsync();
        content = await response.Content.ReadAsStringAsync();
        doc.LoadHtml(content);
        form = doc.DocumentNode.SelectSingleNode("//form");

        // deny consent: the "No, Do Not Allow" submit button posts the model's Button field as "no".
        // FormAction only collects input/textarea/select, so add the button value explicitly.
        formAction = new FormAction(client, form).AddValue("inputModel.Button", "no");

        // Act
        // submitting denial resumes the authorize callback with the denial marker, which makes the
        // pipeline redirect access_denied back to the client's redirect_uri (echoed as json).
        response = await formAction.SubmitAsync();
        var json = await response.Content.ReadAsStringAsync();
        var callbackData = JsonSerializer.Deserialize<Dictionary<string, string>>(json);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(callbackData);
        Assert.Equal("access_denied", callbackData["error"]);
        Assert.Equal("state", callbackData["state"]);
        Assert.DoesNotContain("code", callbackData.Keys);
    }
}
