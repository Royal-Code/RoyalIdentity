using System.Net;
using System.Net.Http.Headers;
using Tests.Integration.Prepare;

namespace Tests.Integration.Endpoints;

/// <summary>
/// The HTTP contract of failures that happen before a request qualifies as a protocol request, across every
/// endpoint that can produce one.
/// </summary>
/// <remarks>
/// DF12: 405, 415 and 404 belong to HTTP, not to the RFC 6749 §5.2 taxonomy. They used to answer an
/// OAuth-shaped body carrying <c>method_not_allowed</c>, <c>Invalid_content_type</c> and <c>not_found</c> —
/// codes no RFC defines. They now answer <c>application/problem+json</c>, and 405 carries the <c>Allow</c>
/// header RFC 9110 §15.5.6 requires. The architectural guard proves the retired codes did not come back; this
/// suite proves the contract that replaced them, on every endpoint rather than only on the token endpoint.
/// </remarks>
public class HttpFailureTests : IClassFixture<PersistentStorageAppFactory>
{
    private readonly PersistentStorageAppFactory factory;

    public HttpFailureTests(PersistentStorageAppFactory factory) => this.factory = factory;

    private string Realm => factory.Handles.Demo.Path;

    public static TheoryData<string, string> MethodNotAllowedCases => new()
    {
        // endpoint kind, expected Allow
        { "token", "POST" },
        { "revocation", "POST" },
        { "discovery", "GET" },
        { "jwks", "GET" },
        { "protected-resource-metadata", "GET" },
        { "authorize-callback", "GET" },
        { "userinfo", "GET, POST" },
        { "end-session", "GET, POST" },
        { "authorize", "GET, POST" },

        // CheckSessionEndpoint is deliberately absent: MapOpenIdConnectProviderEndpoints does not map it, so
        // it has no route to answer 405 from. Mapping it belongs to plan-oidc-session-management.
    };

    private string UrlOf(string endpoint) => endpoint switch
    {
        "token" => Oidc.Routes.BuildTokenUrl(Realm),
        "revocation" => Oidc.Routes.BuildRevocationUrl(Realm),
        "discovery" => Oidc.Routes.BuildDiscoveryConfigurationUrl(Realm),
        "jwks" => Oidc.Routes.BuildDiscoveryWebKeysUrl(Realm),
        "protected-resource-metadata" => Oidc.Routes.BuildProtectedResourceMetadataUrl(Realm),
        "authorize-callback" => Oidc.Routes.BuildAuthorizeCallbackUrl(Realm),
        "userinfo" => Oidc.Routes.BuildUserInfoUrl(Realm),
        "end-session" => Oidc.Routes.BuildEndSessionUrl(Realm),
        "authorize" => Oidc.Routes.BuildAuthorizeUrl(Realm),
        _ => throw new ArgumentOutOfRangeException(nameof(endpoint), endpoint, "Unknown endpoint")
    };

    /// <summary>A method no endpoint under test serves, so every one of them answers 405.</summary>
    private Task<HttpResponseMessage> SendDeleteAsync(string endpoint)
        => factory.CreateClient().SendAsync(new HttpRequestMessage(HttpMethod.Delete, UrlOf(endpoint)));

    private static async Task AssertProblemDetailsAsync(HttpResponseMessage response)
    {
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("\"error\"", body, StringComparison.Ordinal);
        Assert.DoesNotContain("\"error_description\"", body, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(MethodNotAllowedCases))]
    public async Task AnUnsupportedMethod_Must_Answer405WithTheEndpointsAllow(string endpoint, string expectedAllow)
    {
        var response = await SendDeleteAsync(endpoint);

        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
        Assert.Equal(expectedAllow, string.Join(", ", response.Content.Headers.Allow));
    }

    [Theory]
    [MemberData(nameof(MethodNotAllowedCases))]
    public async Task AnUnsupportedMethod_Must_AnswerProblemDetails(string endpoint, string _)
    {
        var response = await SendDeleteAsync(endpoint);

        await AssertProblemDetailsAsync(response);
    }

    [Theory]
    [InlineData("token")]
    [InlineData("revocation")]
    [InlineData("authorize")]
    [InlineData("end-session")]
    public async Task APostWithAWrongContentType_Must_Answer415AsProblemDetails(string endpoint)
    {
        var content = new StringContent("whatever=1");
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

        var response = await factory.CreateClient().PostAsync(UrlOf(endpoint), content);

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
        await AssertProblemDetailsAsync(response);
    }

    /// <summary>
    /// The three callers of <c>EndpointErrors.NotFound</c>, each with the switch that actually turns it off.
    /// Discovery and the protected resource metadata are gated by <c>Endpoints.EnableDiscoveryEndpoint</c>; the
    /// key set has its own switch, <c>Discovery.ShowKeySet</c>, so a single toggle would leave JWKS answering
    /// 200 and the case would prove nothing.
    /// </summary>
    public static TheoryData<string, bool> NotFoundCases => new()
    {
        // endpoint, gated by Endpoints.EnableDiscoveryEndpoint (false means Discovery.ShowKeySet)
        { "discovery", true },
        { "protected-resource-metadata", true },
        { "jwks", false },
    };

    [Theory]
    [MemberData(nameof(NotFoundCases))]
    public async Task ADisabledEndpoint_Must_Answer404AsProblemDetails(string endpoint, bool gatedByDiscoveryEndpoint)
    {
        await SetGateAsync(gatedByDiscoveryEndpoint, enabled: false);
        try
        {
            var response = await factory.CreateClient().GetAsync(UrlOf(endpoint));

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            await AssertProblemDetailsAsync(response);
        }
        finally
        {
            await SetGateAsync(gatedByDiscoveryEndpoint, enabled: true);
        }
    }

    private Task SetGateAsync(bool gatedByDiscoveryEndpoint, bool enabled)
    {
        return factory.UpdateRealmAsync(
            factory.Handles.Demo,
            options =>
            {
                if (gatedByDiscoveryEndpoint)
                    options.Endpoints.EnableDiscoveryEndpoint = enabled;
                else
                    options.Discovery.ShowKeySet = enabled;
            });
    }
}
