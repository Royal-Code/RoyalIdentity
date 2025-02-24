using HtmlAgilityPack;
using RoyalIdentity.Extensions;
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
    public async Task Login_WhenValid_MustShowConsent_WhenClientRequires_ThenRedirectToRequestedOrigin()
    {
        // 1 - Login and Consent
        // Arrange 1

        var client = factory.CreateClient();

        var codeVerifier = CryptoRandom.CreateUniqueId();
        var codeVerifierBytes = Encoding.ASCII.GetBytes(codeVerifier);
        var codeChallenge = Base64Url.Encode(codeVerifierBytes.Sha256());

        var path = "/connect/authorize"
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

        // Act 3
        var tokenResponse = await client.PostAsync("/connect/token", tokenContent);
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
}
