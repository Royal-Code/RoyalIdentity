using System.Net;
using System.Net.Http.Headers;
using System.Text;
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

    /// <summary>A client that authenticates with no secret at all, so a fallback would actually succeed.</summary>
    private async Task<string> SeedPublicClientAsync()
    {
        const string clientId = "token_error_public_client";

        await factory.SaveClientAsync(
            factory.Handles.Demo,
            clientId,
            configured =>
            {
                configured.Name = "Token Error Public Client";
                configured.ClientType = ClientType.Public;
                configured.RequireClientSecret = false;
                configured.AllowedGrantTypes.Clear();
                configured.AllowedGrantTypes.Add("client_credentials");
                configured.AllowedScopes.Add("api");
            });

        return clientId;
    }

    private string TokenUrl => Oidc.Routes.BuildTokenUrl(factory.Handles.Demo.Path);

    private Task<HttpResponseMessage> PostAsync(params KeyValuePair<string, string>[] form)
        => factory.CreateClient().PostAsync(TokenUrl, new FormUrlEncodedContent(form));

    private Task<HttpResponseMessage> PostWithBasicAsync(
        string clientId,
        string clientSecret,
        params KeyValuePair<string, string>[] form)
    {
        var client = factory.CreateClient();
        var pair = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{clientId}:{clientSecret}"));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", pair);

        return client.PostAsync(TokenUrl, new FormUrlEncodedContent(form));
    }

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

    // DF15: nothing about a refused authentication may tell a caller which client identifiers exist. Until
    // Fase 2 an unknown client answered "No client identified" and a wrong secret "Client secret validation
    // failed" — same code, same status, different description, which is a client-existence oracle.
    [Fact]
    public async Task Post_WithUnknownClient_And_WithWrongSecret_Must_AnswerIdentically()
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
        Assert.Equal(unknownClient.Answer, wrongSecret.Answer);
        Assert.Equal(unknownClient.StatusCode, wrongSecret.StatusCode);
    }

    [Fact]
    public async Task Post_WithoutAnyCredential_Must_AnswerLikeAWrongSecret()
    {
        // The third way the same refusal can be reached: the client requires a secret and sent none. RFC 6749
        // §5.2 puts "no client authentication included" under invalid_client, next to "unknown client".
        await SeedClientAsync();

        var noCredential = await (await PostAsync(
            Field("grant_type", "client_credentials"),
            Field("client_id", ClientId))).ReadErrorAsync();

        var wrongSecret = await (await PostAsync(
            Field("grant_type", "client_credentials"),
            Field("client_id", ClientId),
            Field("client_secret", "not-the-secret"))).ReadErrorAsync();

        Assert.Equal(Oidc.Token.Errors.InvalidClient, noCredential.Error);
        Assert.Equal(noCredential.Answer, wrongSecret.Answer);
    }

    // ---------------------------------------------------------------------------------------------------
    // Cardinality of the request parameters (DF8)
    // ---------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData("grant_type")]
    [InlineData("client_id")]
    [InlineData("client_secret")]
    [InlineData("scope")]
    [InlineData("code")]
    [InlineData("code_verifier")]
    [InlineData("redirect_uri")]
    [InlineData("refresh_token")]
    [InlineData("client_assertion")]
    [InlineData("client_assertion_type")]
    public async Task Post_WithARepeatedCoreParameter_Must_AnswerInvalidRequest(string parameter)
    {
        await SeedClientAsync();

        var response = await PostAsync(Authenticated(
            Field("grant_type", "client_credentials"),
            Field(parameter, "first"),
            Field(parameter, "second")));

        await response.AssertErrorAsync(Oidc.Token.Errors.InvalidRequest);
    }

    [Fact]
    public async Task Post_WithARepeatedResource_Must_NotBeRejectedAsMalformed()
    {
        // RFC 8707 §2.1 declares resource repeatable, so the cardinality rule must have an explicit exception
        // for it. Both indicators here are valid, so the request succeeds outright.
        await SeedClientAsync();

        var response = await PostAsync(Authenticated(
            Field("grant_type", "client_credentials"),
            Field("scope", "api"),
            Field("resource", "https://api.demo.local/apiserver"),
            Field("resource", "https://api.demo.local/apiserver")));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // ---------------------------------------------------------------------------------------------------
    // One mechanism per request (DF7/DF15)
    // ---------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Post_WithBasicAndPostSecret_Must_AnswerInvalidRequest()
    {
        await SeedClientAsync();

        var response = await PostWithBasicAsync(
            ClientId,
            ClientSecret,
            Field("grant_type", "client_credentials"),
            Field("client_secret", ClientSecret));

        await response.AssertErrorAsync(Oidc.Token.Errors.InvalidRequest);
    }

    [Fact]
    public async Task Post_WithBasicAndAssertion_Must_AnswerInvalidRequest()
    {
        await SeedClientAsync();

        var response = await PostWithBasicAsync(
            ClientId,
            ClientSecret,
            Field("grant_type", "client_credentials"),
            Field("client_assertion", "not-even-a-jwt"),
            Field("client_assertion_type", Oidc.ClientAssertionTypes.JwtBearer));

        await response.AssertErrorAsync(Oidc.Token.Errors.InvalidRequest);
    }

    [Fact]
    public async Task Post_WithPostSecretAndAssertion_Must_AnswerInvalidRequest()
    {
        await SeedClientAsync();

        var response = await PostAsync(
            Field("grant_type", "client_credentials"),
            Field("client_id", ClientId),
            Field("client_secret", ClientSecret),
            Field("client_assertion", "not-even-a-jwt"),
            Field("client_assertion_type", Oidc.ClientAssertionTypes.JwtBearer));

        await response.AssertErrorAsync(Oidc.Token.Errors.InvalidRequest);
    }

    [Fact]
    public async Task Post_WithAssertionButNoType_Must_AnswerInvalidRequest()
    {
        // Form, not authentication: the pair is incomplete, so no method was selected and RFC 7523 §3.2 does
        // not apply yet (DF15).
        await SeedClientAsync();

        var response = await PostAsync(
            Field("grant_type", "client_credentials"),
            Field("client_assertion", "not-even-a-jwt"));

        await response.AssertErrorAsync(Oidc.Token.Errors.InvalidRequest);
    }

    [Fact]
    public async Task Post_WithAssertionTypeButNoAssertion_Must_AnswerInvalidRequest()
    {
        await SeedClientAsync();

        var response = await PostAsync(
            Field("grant_type", "client_credentials"),
            Field("client_assertion_type", Oidc.ClientAssertionTypes.JwtBearer));

        await response.AssertErrorAsync(Oidc.Token.Errors.InvalidRequest);
    }

    [Fact]
    public async Task Post_WithCompleteAssertionPairOfAnUnsupportedType_Must_AnswerInvalidClient()
    {
        // The pair is complete, so a client authentication method was selected; an unsupported one is an
        // authentication failure, and its description stays as generic as every other invalid_client.
        await SeedClientAsync();

        var response = await PostAsync(
            Field("grant_type", "client_credentials"),
            Field("client_assertion", "not-even-a-jwt"),
            Field("client_assertion_type", "urn:example:unsupported-assertion-type"));

        var error = await response.AssertErrorAsync(Oidc.Token.Errors.InvalidClient);
        Assert.DoesNotContain("assertion", error.Description ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    // ---------------------------------------------------------------------------------------------------
    // The Authorization header decides the status (DF6)
    // ---------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Post_WithInvalidBasicCredentials_Must_Answer401WithAChallenge()
    {
        await SeedClientAsync();

        var response = await PostWithBasicAsync(
            ClientId,
            "not-the-secret",
            Field("grant_type", "client_credentials"));

        var error = await response.AssertErrorAsync(
            Oidc.Token.Errors.InvalidClient, HttpStatusCode.Unauthorized);

        Assert.Equal($"Basic realm=\"{factory.Handles.Demo.Path}\"", error.Headers["WWW-Authenticate"]);
    }

    [Fact]
    public async Task Post_WithMalformedBasicHeader_Must_Answer401WithAChallenge()
    {
        // A header that cannot even be decoded used to fall through to the next evaluator, which for a
        // connection carrying a certificate meant authenticating by certificate instead of refusing.
        await SeedClientAsync();

        var request = new HttpRequestMessage(HttpMethod.Post, TokenUrl)
        {
            Content = new FormUrlEncodedContent(new[] { Field("grant_type", "client_credentials") })
        };
        request.Headers.TryAddWithoutValidation("Authorization", "Basic not-base64!!");

        var response = await factory.CreateClient().SendAsync(request);

        var error = await response.AssertErrorAsync(
            Oidc.Token.Errors.InvalidClient, HttpStatusCode.Unauthorized);

        Assert.Equal($"Basic realm=\"{factory.Handles.Demo.Path}\"", error.Headers["WWW-Authenticate"]);
    }

    // Regression for the gap an external review found: detection keyed on the "Basic " prefix, so an
    // Authorization header the endpoint does not understand was classified as "no credential presented" and
    // fell through to the connection certificate or to the no-secret evaluator — the exact fallback this phase
    // exists to remove. A header present at all is an in-band attempt, whatever scheme it names.
    [Theory]
    [InlineData("Bearer some-token")]
    [InlineData("Basic")]
    [InlineData("Negotiate abc")]
    [InlineData("Digest username=\"x\"")]
    [InlineData("   ")]
    // A header with an entirely empty value has no case here: HttpClient does not transmit one, so the request
    // arrives with no Authorization key at all and is — correctly — an ordinary public client request. The
    // whitespace variant above is the reachable form of the same idea, and it did fall through until the
    // decision stopped looking at the value.
    public async Task Post_WithAnUnusableAuthorizationHeader_Must_NotFallBackToNoSecret(string authorization)
    {
        var clientId = await SeedPublicClientAsync();

        var request = new HttpRequestMessage(HttpMethod.Post, TokenUrl)
        {
            Content = new FormUrlEncodedContent(new[]
            {
                Field("grant_type", "client_credentials"),
                Field("client_id", clientId),
                Field("scope", "api")
            })
        };
        request.Headers.TryAddWithoutValidation("Authorization", authorization);

        var response = await factory.CreateClient().SendAsync(request);

        var error = await response.AssertErrorAsync(
            Oidc.Token.Errors.InvalidClient, HttpStatusCode.Unauthorized);

        Assert.Equal($"Basic realm=\"{factory.Handles.Demo.Path}\"", error.Headers["WWW-Authenticate"]);
    }

    [Fact]
    public async Task Post_WithoutAnyAuthorizationHeader_Must_StillAcceptAPublicClient()
    {
        // The counterpart: refusing an unusable header must not have made every public client unusable.
        var clientId = await SeedPublicClientAsync();

        var response = await PostAsync(
            Field("grant_type", "client_credentials"),
            Field("client_id", clientId),
            Field("scope", "api"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Post_WithTwoAuthorizationHeaders_Must_AnswerInvalidRequest()
    {
        // RFC 9110 §11.6.2 allows a single Authorization header. Two of them is the same malformed cardinality
        // as a repeated core parameter, and picking the first would let the one that authenticates differ from
        // the one that was inspected.
        await SeedClientAsync();

        var request = new HttpRequestMessage(HttpMethod.Post, TokenUrl)
        {
            Content = new FormUrlEncodedContent(new[] { Field("grant_type", "client_credentials") })
        };
        request.Headers.TryAddWithoutValidation("Authorization", "Bearer first");
        request.Headers.TryAddWithoutValidation(
            "Authorization",
            $"Basic {Convert.ToBase64String(Encoding.UTF8.GetBytes($"{ClientId}:{ClientSecret}"))}");

        var response = await factory.CreateClient().SendAsync(request);

        await response.AssertErrorAsync(Oidc.Token.Errors.InvalidRequest);
    }

    [Fact]
    public async Task Post_WithInvalidPostSecret_Must_Answer400WithoutAChallenge()
    {
        // The same failure through the body is a plain 400: there is no header attempt to challenge.
        await SeedClientAsync();

        var response = await PostAsync(
            Field("grant_type", "client_credentials"),
            Field("client_id", ClientId),
            Field("client_secret", "not-the-secret"));

        var error = await response.AssertErrorAsync(Oidc.Token.Errors.InvalidClient);

        Assert.False(error.Headers.ContainsKey("WWW-Authenticate"));
    }

    [Fact]
    public async Task Post_WithInvalidBasicCredentials_Must_AnswerLikeAnInvalidPostSecret_ExceptForTheStatus()
    {
        // The challenge is the only thing the transport changes: the classification and the description must
        // not leak which mechanism was used to guess a credential.
        await SeedClientAsync();

        var viaHeader = await (await PostWithBasicAsync(
            ClientId, "not-the-secret", Field("grant_type", "client_credentials"))).ReadErrorAsync();

        var viaBody = await (await PostAsync(
            Field("grant_type", "client_credentials"),
            Field("client_id", ClientId),
            Field("client_secret", "not-the-secret"))).ReadErrorAsync();

        Assert.Equal(viaHeader.Answer, viaBody.Answer);
        Assert.Equal(HttpStatusCode.Unauthorized, viaHeader.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, viaBody.StatusCode);
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

    // DF9: a required parameter that is absent never became a grant, so it is a malformed request rather than
    // an invalid one. This answered invalid_grant until Fase 3.
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task Post_WithoutAuthorizationCode_Must_AnswerInvalidRequest(string? code)
    {
        await SeedClientAsync();

        KeyValuePair<string, string>[] extra = code is null
            ? [Field("grant_type", "authorization_code"), Field("redirect_uri", "http://localhost:5000/callback")]
            : [
                Field("grant_type", "authorization_code"),
                Field("code", code),
                Field("redirect_uri", "http://localhost:5000/callback")
            ];

        var response = await PostAsync(Authenticated(extra));

        await response.AssertErrorAsync(Oidc.Token.Errors.InvalidRequest);
    }

    [Fact]
    public async Task Post_WithATooLongAuthorizationCode_Must_AnswerInvalidRequest()
    {
        await SeedClientAsync();

        var response = await PostAsync(Authenticated(
            Field("grant_type", "authorization_code"),
            Field("code", new string('c', 1024)),
            Field("redirect_uri", "http://localhost:5000/callback")));

        await response.AssertErrorAsync(Oidc.Token.Errors.InvalidRequest);
    }

    [Fact]
    public async Task Post_WithoutRefreshToken_Must_AnswerInvalidRequest_LikeAMissingCode()
    {
        // The two grants classify a missing required parameter the same way. LoadRefreshToken was already
        // right; LoadCode is what Fase 3 brought into line, and this pins them together.
        await SeedClientAsync();

        var missingCode = await (await PostAsync(Authenticated(
            Field("grant_type", "authorization_code"),
            Field("redirect_uri", "http://localhost:5000/callback")))).ReadErrorAsync();

        var missingRefresh = await (await PostAsync(Authenticated(
            Field("grant_type", "refresh_token")))).ReadErrorAsync();

        Assert.Equal(Oidc.Token.Errors.InvalidRequest, missingCode.Error);
        Assert.Equal(missingCode.Error, missingRefresh.Error);
    }

    // DF10: the client authenticated fine and the server implements the grant; what fails is the client's
    // authorization to use it. This answered invalid_grant until Fase 3.
    [Fact]
    public async Task Post_WithGrantNotAllowedForTheClient_Must_AnswerUnauthorizedClient()
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

        await response.AssertErrorAsync(Oidc.Token.Errors.UnauthorizedClient);
    }

    [Fact]
    public async Task Post_WithGrantNotAllowed_Must_NotBeConfusedWithAnUnimplementedGrant()
    {
        // unauthorized_client and unsupported_grant_type answer different questions: "you may not use this"
        // versus "this server does not have it". Collapsing them would hide a misconfigured client behind a
        // message about the server's capabilities.
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

        var notAllowed = await PostAsync(
            Field("grant_type", "refresh_token"),
            Field("refresh_token", "whatever"),
            Field("client_id", clientId),
            Field("client_secret", clientSecret));

        var notImplemented = await PostAsync(
            Field("grant_type", "urn:example:no-such-grant"),
            Field("client_id", clientId),
            Field("client_secret", clientSecret));

        await notAllowed.AssertErrorAsync(Oidc.Token.Errors.UnauthorizedClient);
        await notImplemented.AssertErrorAsync(Oidc.Token.Errors.UnsupportedGrantType);
    }

    // ---------------------------------------------------------------------------------------------------
    // HTTP before the protocol
    // ---------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Get_Must_Answer405WithAllow()
    {
        var response = await factory.CreateClient().GetAsync(TokenUrl);

        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
        Assert.Equal("POST", string.Join(", ", response.Content.Headers.Allow));
    }

    [Theory]
    [InlineData("GET")]
    [InlineData("text/plain")]
    public async Task HttpLevelFailures_Must_NotBeShapedLikeAProtocolError(string trigger)
    {
        // DF12: these happen before the request qualifies as a protocol request, so they answer
        // application/problem+json and carry no error field — the invented method_not_allowed and
        // Invalid_content_type codes claimed a membership in the RFC 6749 §5.2 taxonomy they never had.
        var http = factory.CreateClient();

        HttpResponseMessage response;
        if (trigger is "GET")
        {
            response = await http.GetAsync(TokenUrl);
        }
        else
        {
            var content = new StringContent("grant_type=client_credentials");
            content.Headers.ContentType = new MediaTypeHeaderValue(trigger);
            response = await http.PostAsync(TokenUrl, content);
        }

        var body = await response.Content.ReadAsStringAsync();

        Assert.Contains("problem+json", response.Content.Headers.ContentType?.MediaType ?? string.Empty);
        Assert.DoesNotContain("\"error\"", body, StringComparison.Ordinal);
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
