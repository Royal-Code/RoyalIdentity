using System.Text.Json;
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
    }
}