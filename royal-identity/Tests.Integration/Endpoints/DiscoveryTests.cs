using System.Text.Json;
using RoyalIdentity.Extensions;
using RoyalIdentity.Models.Scopes;
using RoyalIdentity.Utils;
using Tests.Integration.Prepare;

namespace Tests.Integration.Endpoints;

public class DiscoveryTests : IClassFixture<PersistentStorageAppFactory>
{
    private readonly PersistentStorageAppFactory factory;

    public DiscoveryTests(PersistentStorageAppFactory factory)
    {
        this.factory = factory;
    }

    [Fact]
    public async Task Get_Must_ReturnsTheJsonDocument()
    {
        // Arrange
        var client = factory.CreateClient();
        var url = Oidc.Routes.BuildDiscoveryConfigurationUrl(factory.Handles.Demo.Path);

        // Act
        var response = await client.GetAsync(url);

        // Assert
        Assert.NotNull(response);
        Assert.True(response.IsSuccessStatusCode);

        var content = await response.Content.ReadAsStringAsync();
        Assert.NotNull(content);

        var document = JsonSerializer.Deserialize<Dictionary<string, object>>(content);
        Assert.NotNull(document);

        Assert.Contains("issuer", document);
        Assert.Contains("authorization_endpoint", document);
        Assert.Contains("token_endpoint", document);
        Assert.Contains("userinfo_endpoint", document);
        Assert.Contains("end_session_endpoint", document);
        Assert.Contains("jwks_uri", document);
        Assert.Contains("scopes_supported", document);
        Assert.Contains("response_types_supported", document);
        Assert.Contains("response_modes_supported", document);
        Assert.Contains("grant_types_supported", document);
        Assert.Contains("subject_types_supported", document);
        Assert.Contains("id_token_signing_alg_values_supported", document);
        Assert.Contains("token_endpoint_auth_methods_supported", document);
        Assert.Contains("claims_supported", document);
        Assert.Contains("code_challenge_methods_supported", document);
        Assert.Contains("protected_resources", document);
    }

    [Fact]
    public async Task Get_ShouldPublishProtectedResources()
    {
        var client = factory.CreateClient();
        var url = Oidc.Routes.BuildDiscoveryConfigurationUrl(factory.Handles.Demo.Path);

        var response = await client.GetAsync(url);
        var content = await response.Content.ReadAsStringAsync();
        var document = JsonDocument.Parse(content);

        Assert.True(document.RootElement.TryGetProperty("protected_resources", out var protectedResources));
        Assert.Contains(
            "https://api.demo.local/apiserver",
            protectedResources.EnumerateArray().Select(resource => resource.GetString()));
    }

    [Fact]
    public async Task Get_ScopesSupported_ShouldExcludeScopesOfResourceServersThatDisallowScopeRequests()
    {
        // ADR-012: scopes of resource servers with AllowScopeRequests = false are not requestable via the
        // scope parameter, so they must not be advertised in scopes_supported (reachable only via resource).
        var suffix = CryptoRandom.CreateUniqueId(4, OutputFormat.Hex);
        var serverName = $"audience-only-discovery-{suffix}";
        var hiddenScope = $"{serverName}:read";

        factory.Resources.SetResourceServer(
            factory.Handles.Demo.Id,
            new ResourceServer(
            ScopeVisibility.Public, serverName, "Audience Only API", "Audience Only API")
            {
                AllowScopeRequests = false,
                Scopes = [new Scope(ScopeVisibility.Public, hiddenScope, "read", "read")]
            });

        var client = factory.CreateClient();
        var url = Oidc.Routes.BuildDiscoveryConfigurationUrl(factory.Handles.Demo.Path);

        var response = await client.GetAsync(url);
        var content = await response.Content.ReadAsStringAsync();
        var document = JsonDocument.Parse(content);

        Assert.True(document.RootElement.TryGetProperty("scopes_supported", out var scopesSupported));
        var scopes = scopesSupported.EnumerateArray().Select(scope => scope.GetString()).ToList();
        // a normal (AllowScopeRequests = true) scope is still advertised as a control
        Assert.Contains("api:read", scopes);
        // the audience-only scope is excluded
        Assert.DoesNotContain(hiddenScope, scopes);
    }

    [Fact]
    public async Task Get_ProtectedResourceMetadata_ShouldReturnRfc9728Document()
    {
        var client = factory.CreateClient();
        var url = Oidc.Routes.BuildProtectedResourceMetadataUrl(factory.Handles.Demo.Path)
            .AddQueryString("resource", "https://api.demo.local/apiserver");

        var response = await client.GetAsync(url);
        var content = await response.Content.ReadAsStringAsync();
        var document = JsonDocument.Parse(content);
        var root = document.RootElement;

        Assert.True(response.IsSuccessStatusCode);
        Assert.Equal("https://api.demo.local/apiserver", root.GetProperty("resource").GetString());
        Assert.Equal("API Server", root.GetProperty("resource_name").GetString());
        Assert.Contains(
            "api:read",
            root.GetProperty("scopes_supported").EnumerateArray().Select(scope => scope.GetString()));
        Assert.Contains(
            "header",
            root.GetProperty("bearer_methods_supported").EnumerateArray().Select(method => method.GetString()));
        Assert.NotEmpty(root.GetProperty("authorization_servers").EnumerateArray());
    }

    [Fact]
    public async Task Get_ProtectedResourceMetadata_WithUnknownResource_ShouldReturnInvalidTarget()
    {
        var client = factory.CreateClient();
        var url = Oidc.Routes.BuildProtectedResourceMetadataUrl(factory.Handles.Demo.Path)
            .AddQueryString("resource", "https://unknown.example.test/resource");

        var response = await client.GetAsync(url);

        await response.AssertErrorAsync(Oidc.Token.Errors.InvalidTarget);
    }

    [Fact]
    public async Task Get_Must_AnnounceExactlyTheAuthenticationMethodsThatAreTested()
    {
        // The announcement is a promise, and a promise nothing verifies is how a mechanism silently stops
        // working. Pinning the exact set means adding a method to discovery — or losing one to a refactor of
        // the evaluator chain — cannot happen without this test and the success case behind each entry.
        //
        // Covered by: client_secret_basic in TokenErrorTests.Post_WithValidBasicCredentials_Must_IssueAToken,
        // client_secret_post throughout ClientTokenTests, and private_key_jwt in
        // PrivateKeyJwtReplayProtectionTests.FirstPresentation_IsAccepted_AndTheSamePresentationAgainIsRefused.
        //
        // This is the default realm configuration. The two mTLS methods are announced only when
        // MutualTls.Enabled is set — see the test below, and the handoff recorded there.
        var client = factory.CreateClient();
        var url = Oidc.Routes.BuildDiscoveryConfigurationUrl(factory.Handles.Demo.Path);

        var content = await client.GetStringAsync(url);
        var document = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(content);

        var methods = document![Oidc.Discovery.TokenEndpointAuthenticationMethodsSupported]
            .EnumerateArray()
            .Select(value => value.GetString())
            .ToArray();

        Assert.Equal(
            [
                Oidc.Endpoint.AuthMethods.BasicAuthentication,
                Oidc.Endpoint.AuthMethods.PostBody,
                Oidc.Endpoint.AuthMethods.PrivateKeyJwt
            ],
            methods);
    }

    /// <summary>
    /// With mTLS enabled the announcement gains two methods that <b>no test proves a client can authenticate
    /// with</b>.
    /// </summary>
    /// <remarks>
    /// Exercising them needs a client certificate presented through the connection, which this in-memory test
    /// server does not provide, so this test pins the composition only: the two names are added on top of the
    /// three, and adding a third one silently is not possible. Proving they authenticate belongs to
    /// <c>plan-rfc9700-security-hardening.md</c>, which owns the mTLS metadata and aliases and has the task
    /// recorded.
    /// </remarks>
    [Fact]
    public async Task Get_WithMutualTlsEnabled_Must_AnnounceTheTwoMtlsMethodsOnTop()
    {
        await factory.UpdateRealmAsync(
            factory.Handles.Demo,
            options => options.MutualTls.Enabled = true);
        try
        {
            var client = factory.CreateClient();
            var url = Oidc.Routes.BuildDiscoveryConfigurationUrl(factory.Handles.Demo.Path);

            var content = await client.GetStringAsync(url);
            var document = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(content);

            var methods = document![Oidc.Discovery.TokenEndpointAuthenticationMethodsSupported]
                .EnumerateArray()
                .Select(value => value.GetString())
                .ToArray();

            Assert.Equal(
                [
                    Oidc.Endpoint.AuthMethods.BasicAuthentication,
                    Oidc.Endpoint.AuthMethods.PostBody,
                    Oidc.Endpoint.AuthMethods.PrivateKeyJwt,
                    Oidc.Endpoint.AuthMethods.TlsClientAuth,
                    Oidc.Endpoint.AuthMethods.SelfSignedTlsClientAuth
                ],
                methods);
        }
        finally
        {
            await factory.UpdateRealmAsync(
                factory.Handles.Demo,
                options => options.MutualTls.Enabled = false);
        }
    }
}
