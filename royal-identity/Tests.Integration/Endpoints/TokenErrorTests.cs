using System.Net;
using System.Net.Http.Headers;
using RoyalIdentity.Extensions;
using RoyalIdentity.Models;
using RoyalIdentity.Utils;
using Tests.Integration.Prepare;

namespace Tests.Integration.Endpoints;

/// <summary>
/// The HTTP matrix of the token endpoint: for every observable condition, the exact value of the JSON
/// <c>error</c> field and the exact status.
/// </summary>
/// <remarks>
/// <para>
/// Every case reads the payload through <see cref="ProtocolErrorResponse"/>, so a test only passes when the
/// expected code really is the <c>error</c> field — never when it merely appears inside
/// <c>error_description</c> (DF2).
/// </para>
/// <para>
/// This is the Fase 1 baseline. The rows marked below still assert the classification the server has today and
/// are corrected by Fase 3 (grant authorization and PKCE) — they are asserted rather than omitted so the change
/// of behavior shows up as an intentional edit of this file.
/// </para>
/// </remarks>
public class TokenErrorTests : IClassFixture<PersistentStorageAppFactory>
{
    private const string ClientId = "token_error_client";
    private const string ClientSecret = "token_error_client_secret";

    private readonly PersistentStorageAppFactory factory;

    public TokenErrorTests(PersistentStorageAppFactory factory) => this.factory = factory;

    private Task SeedClientAsync()
    {
        return factory.SaveClientAsync(
            factory.Handles.Demo,
            ClientId,
            configured =>
            {
                configured.Name = "Token Error Client";
                configured.ClientType = ClientType.Confidential;
                configured.RequireClientSecret = true;
                configured.AllowedGrantTypes.Clear();
                configured.AllowedGrantTypes.UnionWith(
                    ["client_credentials", "authorization_code", "refresh_token"]);
                configured.Secrets.Add(new ClientSecret(ClientSecret.Sha512()));
                configured.AllowedScopes.Add("api");
                configured.AllowedResourceServers.Add("apiserver");
                configured.AllowedResponseTypes.Add("code");
                configured.RedirectUris.Add("http://localhost:5000/callback");
            });
    }

    private string TokenUrl => Oidc.Routes.BuildTokenUrl(factory.Handles.Demo.Path);

    private Task<HttpResponseMessage> PostAsync(params KeyValuePair<string, string>[] form)
        => factory.CreateClient().PostAsync(TokenUrl, new FormUrlEncodedContent(form));

    private static KeyValuePair<string, string> Field(string name, string value) => new(name, value);

    /// <summary>Authenticated client_credentials request, so only the tested parameter is at fault.</summary>
    private static KeyValuePair<string, string>[] Authenticated(params KeyValuePair<string, string>[] extra)
        => [Field("client_id", ClientId), Field("client_secret", ClientSecret), .. extra];

    // ---------------------------------------------------------------------------------------------------
    // Request form and grant dispatch
    // ---------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Post_WithoutGrantType_Must_AnswerInvalidRequest()
    {
        await SeedClientAsync();

        var response = await PostAsync(Authenticated());

        await response.AssertErrorAsync(Oidc.Token.Errors.InvalidRequest);
    }

    [Fact]
    public async Task Post_WithTooLongGrantType_Must_AnswerInvalidRequest()
    {
        await SeedClientAsync();

        var response = await PostAsync(Authenticated(Field("grant_type", new string('g', 500))));

        await response.AssertErrorAsync(Oidc.Token.Errors.InvalidRequest);
    }

    [Fact]
    public async Task Post_WithUnknownGrantType_Must_AnswerUnsupportedGrantType()
    {
        await SeedClientAsync();

        var response = await PostAsync(Authenticated(Field("grant_type", "urn:example:no-such-grant")));

        await response.AssertErrorAsync(Oidc.Token.Errors.UnsupportedGrantType);
    }

    // ---------------------------------------------------------------------------------------------------
    // Client authentication
    // ---------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Post_WithoutClientAuthentication_Must_AnswerInvalidClient()
    {
        await SeedClientAsync();

        var response = await PostAsync(Field("grant_type", "client_credentials"));

        await response.AssertErrorAsync(Oidc.Token.Errors.InvalidClient);
    }

    [Fact]
    public async Task Post_WithWrongClientSecret_Must_AnswerInvalidClient()
    {
        await SeedClientAsync();

        var response = await PostAsync(
            Field("grant_type", "client_credentials"),
            Field("client_id", ClientId),
            Field("client_secret", "not-the-secret"));

        await response.AssertErrorAsync(Oidc.Token.Errors.InvalidClient);
    }

    [Fact]
    public async Task Post_WithUnknownClient_Must_AnswerInvalidClient()
    {
        await SeedClientAsync();

        var response = await PostAsync(
            Field("grant_type", "client_credentials"),
            Field("client_id", $"unknown-{CryptoRandom.CreateUniqueId(6)}"),
            Field("client_secret", ClientSecret));

        await response.AssertErrorAsync(Oidc.Token.Errors.InvalidClient);
    }

    // Fase 1 baseline: both already converge on the same code and status, which is what this phase owns.
    // They do NOT yet converge on the description — an unknown client answers "No client identified" and a
    // wrong secret "Client secret validation failed", which is a client-existence oracle. Making the whole
    // answer indistinguishable is Fase 2 (DF15), and this assertion is tightened to Answer equality there.
    [Fact]
    public async Task Post_WithUnknownClient_And_WithWrongSecret_Must_ShareCodeAndStatus()
    {
        await SeedClientAsync();

        var unknownClient = await (await PostAsync(
            Field("grant_type", "client_credentials"),
            Field("client_id", $"unknown-{CryptoRandom.CreateUniqueId(6)}"),
            Field("client_secret", ClientSecret))).ReadErrorAsync();

        var wrongSecret = await (await PostAsync(
            Field("grant_type", "client_credentials"),
            Field("client_id", ClientId),
            Field("client_secret", "not-the-secret"))).ReadErrorAsync();

        Assert.Equal(Oidc.Token.Errors.InvalidClient, unknownClient.Error);
        Assert.Equal(unknownClient.Error, wrongSecret.Error);
        Assert.Equal(unknownClient.StatusCode, wrongSecret.StatusCode);
    }

    // ---------------------------------------------------------------------------------------------------
    // Scopes and resources — the field these used to be missing from
    // ---------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Post_WithScopeNotAllowed_Must_AnswerInvalidScopeInTheErrorField()
    {
        await SeedClientAsync();

        var response = await PostAsync(Authenticated(
            Field("grant_type", "client_credentials"),
            Field("scope", "not-an-allowed-scope")));

        var error = await response.AssertErrorAsync(Oidc.Token.Errors.InvalidScope);

        // The code moved out of the description: it is now the classification and not a diagnostic string.
        Assert.DoesNotContain(Oidc.Token.Errors.InvalidScope, error.Description ?? string.Empty);
    }

    [Fact]
    public async Task Post_WithUnknownResourceIndicator_Must_AnswerInvalidTargetInTheErrorField()
    {
        await SeedClientAsync();

        var response = await PostAsync(Authenticated(
            Field("grant_type", "client_credentials"),
            Field("scope", "api"),
            Field("resource", "https://unknown.example/api")));

        var error = await response.AssertErrorAsync(Oidc.Token.Errors.InvalidTarget);

        Assert.DoesNotContain(Oidc.Token.Errors.InvalidTarget, error.Description ?? string.Empty);
    }

    [Fact]
    public async Task Post_WithRepeatedResource_Must_KeepEveryValue()
    {
        // RFC 8707: resource is explicitly repeatable and must survive whatever the request validation does.
        await SeedClientAsync();

        var response = await PostAsync(Authenticated(
            Field("grant_type", "client_credentials"),
            Field("scope", "api"),
            Field("resource", "https://api.demo.local/apiserver"),
            Field("resource", "https://unknown.example/api")));

        // The second indicator is unknown, so the request is refused — but for the target, never because
        // repeating the parameter was itself treated as malformed.
        await response.AssertErrorAsync(Oidc.Token.Errors.InvalidTarget);
    }

    // ---------------------------------------------------------------------------------------------------
    // Grant values
    // ---------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Post_WithoutRefreshToken_Must_AnswerInvalidRequest()
    {
        await SeedClientAsync();

        var response = await PostAsync(Authenticated(Field("grant_type", "refresh_token")));

        await response.AssertErrorAsync(Oidc.Token.Errors.InvalidRequest);
    }

    [Fact]
    public async Task Post_WithUnknownRefreshToken_Must_AnswerInvalidGrant()
    {
        await SeedClientAsync();

        var response = await PostAsync(Authenticated(
            Field("grant_type", "refresh_token"),
            Field("refresh_token", "refresh-token-that-was-never-issued")));

        await response.AssertErrorAsync(Oidc.Token.Errors.InvalidGrant);
    }

    [Fact]
    public async Task Post_WithUnknownAuthorizationCode_Must_AnswerInvalidGrant()
    {
        await SeedClientAsync();

        var response = await PostAsync(Authenticated(
            Field("grant_type", "authorization_code"),
            Field("code", "code-that-was-never-issued"),
            Field("redirect_uri", "http://localhost:5000/callback")));

        await response.AssertErrorAsync(Oidc.Token.Errors.InvalidGrant);
    }

    // Fase 1 baseline, corrected by Fase 3: a missing required parameter belongs to invalid_request, but the
    // server still classifies it as invalid_grant today (LoadCode). Asserting the current value keeps the
    // change visible instead of silent.
    [Fact]
    public async Task Post_WithoutAuthorizationCode_Must_AnswerInvalidGrant_UntilFase3()
    {
        await SeedClientAsync();

        var response = await PostAsync(Authenticated(
            Field("grant_type", "authorization_code"),
            Field("redirect_uri", "http://localhost:5000/callback")));

        await response.AssertErrorAsync(Oidc.Token.Errors.InvalidGrant);
    }

    // Fase 1 baseline, corrected by Fase 3: an authenticated client that may not use the grant belongs to
    // unauthorized_client (GrantTypeValidator).
    [Fact]
    public async Task Post_WithGrantNotAllowedForTheClient_Must_AnswerInvalidGrant_UntilFase3()
    {
        var clientId = $"single-grant-client-{CryptoRandom.CreateUniqueId(6)}";
        var clientSecret = CryptoRandom.CreateUniqueId();
        await factory.SaveClientAsync(
            factory.Handles.Demo,
            clientId,
            configured =>
            {
                configured.Name = "Single Grant Client";
                configured.ClientType = ClientType.Confidential;
                configured.RequireClientSecret = true;
                configured.AllowedGrantTypes.Clear();
                configured.AllowedGrantTypes.Add("client_credentials");
                configured.Secrets.Add(new ClientSecret(clientSecret.Sha512()));
            });

        var response = await PostAsync(
            Field("grant_type", "refresh_token"),
            Field("refresh_token", "whatever"),
            Field("client_id", clientId),
            Field("client_secret", clientSecret));

        await response.AssertErrorAsync(Oidc.Token.Errors.InvalidGrant);
    }

    // ---------------------------------------------------------------------------------------------------
    // HTTP before the protocol
    // ---------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Get_Must_Answer405()
    {
        var response = await factory.CreateClient().GetAsync(TokenUrl);

        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
    }

    [Fact]
    public async Task Post_WithWrongContentType_Must_Answer415()
    {
        var content = new StringContent("grant_type=client_credentials");
        content.Headers.ContentType = new MediaTypeHeaderValue("text/plain");

        var response = await factory.CreateClient().PostAsync(TokenUrl, content);

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
    }

    // ---------------------------------------------------------------------------------------------------
    // Response shape shared by every protocol error
    // ---------------------------------------------------------------------------------------------------

    [Fact]
    public async Task EveryProtocolError_Must_BeUncacheableJson()
    {
        await SeedClientAsync();

        var response = await PostAsync(Authenticated(Field("grant_type", "urn:example:no-such-grant")));
        var error = await response.ReadErrorAsync();

        Assert.Equal("application/json; charset=UTF-8", error.ContentType);
        Assert.Equal("no-store, no-cache, max-age=0", error.CacheControl);
        Assert.Equal("no-cache", error.Headers["Pragma"]);
    }

    [Fact]
    public async Task EveryProtocolError_Must_NotEchoTheClientSecret()
    {
        await SeedClientAsync();

        var response = await PostAsync(
            Field("grant_type", "client_credentials"),
            Field("client_id", ClientId),
            Field("client_secret", "a-very-recognizable-secret"));

        var body = await response.Content.ReadAsStringAsync();

        Assert.DoesNotContain("a-very-recognizable-secret", body, StringComparison.Ordinal);
    }
}
