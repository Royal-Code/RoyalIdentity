using System.Net;
using System.Net.Http.Headers;
using System.Text;
using RoyalIdentity.Extensions;
using RoyalIdentity.Models;
using Tests.Integration.Prepare;

namespace Tests.Integration.Endpoints;

public class RevocationTests : IClassFixture<PersistentStorageAppFactory>
{
    private readonly PersistentStorageAppFactory factory;

    public RevocationTests(PersistentStorageAppFactory factory)
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
        var url = Oidc.Routes.BuildRevocationUrl(factory.Handles.Demo.Path);

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
        var url = Oidc.Routes.BuildRevocationUrl(factory.Handles.Demo.Path);

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
        var url = Oidc.Routes.BuildRevocationUrl(factory.Handles.Demo.Path);

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
        var url = Oidc.Routes.BuildRevocationUrl(factory.Handles.Demo.Path);

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
        var url = Oidc.Routes.BuildRevocationUrl(factory.Handles.Demo.Path);

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
        var url = Oidc.Routes.BuildRevocationUrl(factory.Handles.Demo.Path);

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
        var url = Oidc.Routes.BuildRevocationUrl(factory.Handles.Demo.Path);

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
        var url = Oidc.Routes.BuildRevocationUrl(factory.Handles.Demo.Path);

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

    // -----------------------------------------------------------------------------------------------------
    // Request shape and client authentication (Fase 2). Revocation is the second endpoint whose pipeline
    // reaches EvaluateClient, so it depends on the same preflight as the token endpoint and needs the same
    // negative coverage. RFC 7009 §2.2 makes an unknown token a success, which is what makes "accepted" and
    // "refused" cleanly distinguishable here.
    // -----------------------------------------------------------------------------------------------------

    private const string ConfidentialClientId = "revocation_confidential_client";
    private const string ConfidentialClientSecret = "revocation_confidential_client_secret";

    private string RevocationUrl => Oidc.Routes.BuildRevocationUrl(factory.Handles.Demo.Path);

    private Task SeedConfidentialClientAsync()
    {
        return factory.SaveClientAsync(
            factory.Handles.Demo,
            ConfidentialClientId,
            configured =>
            {
                configured.Name = "Revocation Confidential Client";
                configured.ClientType = ClientType.Confidential;
                configured.RequireClientSecret = true;
                configured.Secrets.Add(new ClientSecret(ConfidentialClientSecret.Sha512()));
            });
    }

    private Task<HttpResponseMessage> PostAsync(params KeyValuePair<string, string>[] form)
        => factory.CreateClient().PostAsync(RevocationUrl, new FormUrlEncodedContent(form));

    private static KeyValuePair<string, string> Field(string name, string value) => new(name, value);

    [Theory]
    [InlineData("token")]
    [InlineData("token_type_hint")]
    [InlineData("client_id")]
    [InlineData("client_secret")]
    public async Task Post_WithARepeatedParameter_Must_AnswerInvalidRequest(string parameter)
    {
        await SeedConfidentialClientAsync();

        var response = await PostAsync(
            Field("token", "AAA"),
            Field("client_id", ConfidentialClientId),
            Field("client_secret", ConfidentialClientSecret),
            Field(parameter, "first"),
            Field(parameter, "second"));

        await response.AssertErrorAsync(Oidc.Token.Errors.InvalidRequest);
    }

    [Fact]
    public async Task Post_WithTwoMechanisms_Must_AnswerInvalidRequest()
    {
        await SeedConfidentialClientAsync();

        var http = factory.CreateClient();
        var pair = Convert.ToBase64String(
            Encoding.UTF8.GetBytes($"{ConfidentialClientId}:{ConfidentialClientSecret}"));
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", pair);

        var response = await http.PostAsync(RevocationUrl, new FormUrlEncodedContent(new[]
        {
            Field("token", "AAA"),
            Field("client_secret", ConfidentialClientSecret)
        }));

        await response.AssertErrorAsync(Oidc.Token.Errors.InvalidRequest);
    }

    [Fact]
    public async Task Post_WithAnIncompleteAssertionPair_Must_AnswerInvalidRequest()
    {
        await SeedConfidentialClientAsync();

        var response = await PostAsync(
            Field("token", "AAA"),
            Field("client_assertion", "not-even-a-jwt"));

        await response.AssertErrorAsync(Oidc.Token.Errors.InvalidRequest);
    }

    [Fact]
    public async Task Post_WithInvalidBasicCredentials_Must_Answer401WithAChallenge()
    {
        await SeedConfidentialClientAsync();

        var http = factory.CreateClient();
        var pair = Convert.ToBase64String(
            Encoding.UTF8.GetBytes($"{ConfidentialClientId}:not-the-secret"));
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", pair);

        var response = await http.PostAsync(RevocationUrl, new FormUrlEncodedContent(new[]
        {
            Field("token", "AAA")
        }));

        var error = await response.AssertErrorAsync(
            Oidc.Token.Errors.InvalidClient, HttpStatusCode.Unauthorized);

        Assert.Equal($"Basic realm=\"{factory.Handles.Demo.Path}\"", error.Headers["WWW-Authenticate"]);
    }

    [Fact]
    public async Task Post_WithoutTheRequiredSecret_Must_AnswerInvalidClient()
    {
        await SeedConfidentialClientAsync();

        var response = await PostAsync(
            Field("token", "AAA"),
            Field("client_id", ConfidentialClientId));

        await response.AssertErrorAsync(Oidc.Token.Errors.InvalidClient);
    }

    // The gap an external review found, on the endpoint where it bites hardest: demo_client is public, so an
    // Authorization header classified as "nothing presented" would have been revoked against successfully.
    [Theory]
    [InlineData("Bearer some-token")]
    [InlineData("Basic")]
    public async Task Post_WithAnUnusableAuthorizationHeader_Must_NotFallBackToNoSecret(string authorization)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, RevocationUrl)
        {
            Content = new FormUrlEncodedContent(new[]
            {
                Field("token", "AAA"),
                Field("client_id", "demo_client")
            })
        };
        request.Headers.TryAddWithoutValidation("Authorization", authorization);

        var response = await factory.CreateClient().SendAsync(request);

        await response.AssertErrorAsync(Oidc.Token.Errors.InvalidClient, HttpStatusCode.Unauthorized);
    }
}
