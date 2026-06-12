using System.Text.Json;
using RoyalIdentity.Extensions;
using Tests.Integration.Prepare;

namespace Tests.Integration.Endpoints;

public class DiscoveryTests : IClassFixture<AppFactory>
{
    private readonly AppFactory factory;

    public DiscoveryTests(AppFactory factory)
    {
        this.factory = factory;
    }

    [Fact]
    public async Task Get_Must_ReturnsTheJsonDocument()
    {
        // Arrange
        var client = factory.CreateClient();
        var url = Oidc.Routes.BuildDiscoveryConfigurationUrl(MemoryStorage.DemoRealm.Path);

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
        var url = Oidc.Routes.BuildDiscoveryConfigurationUrl(MemoryStorage.DemoRealm.Path);

        var response = await client.GetAsync(url);
        var content = await response.Content.ReadAsStringAsync();
        var document = JsonDocument.Parse(content);

        Assert.True(document.RootElement.TryGetProperty("protected_resources", out var protectedResources));
        Assert.Contains(
            "https://api.demo.local/apiserver",
            protectedResources.EnumerateArray().Select(resource => resource.GetString()));
    }

    [Fact]
    public async Task Get_ProtectedResourceMetadata_ShouldReturnRfc9728Document()
    {
        var client = factory.CreateClient();
        var url = Oidc.Routes.BuildProtectedResourceMetadataUrl(MemoryStorage.DemoRealm.Path)
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
        var url = Oidc.Routes.BuildProtectedResourceMetadataUrl(MemoryStorage.DemoRealm.Path)
            .AddQueryString("resource", "https://unknown.example.test/resource");

        var response = await client.GetAsync(url);
        var body = await response.Content.ReadAsStringAsync();

        Assert.False(response.IsSuccessStatusCode);
        Assert.Contains("invalid_target", body);
    }
}
