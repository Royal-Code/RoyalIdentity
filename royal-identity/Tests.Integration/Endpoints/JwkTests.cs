﻿using Microsoft.IdentityModel.Tokens;
using Tests.Integration.Prepare;

namespace Tests.Integration.Endpoints;

public class JwkTests : IClassFixture<AppFactory>
{
    private readonly AppFactory factory;

    public JwkTests(AppFactory factory)
    {
        this.factory = factory;
    }

    [Fact]
    public async Task Get_Must_ReturnTheKeys()
    {
        // Arrange
        var client = factory.CreateClient();
        var url = Oidc.Routes.BuildDiscoveryWebKeysUrl(MemoryStorage.DemoRealm.Path);

        // Act
        var response = await client.GetAsync(url);

        // Assert
        Assert.NotNull(response);
        Assert.True(response.IsSuccessStatusCode);

        var content = await response.Content.ReadAsStringAsync();
        Assert.NotNull(content);

        var key = JsonWebKey.Create(content);
        Assert.NotNull(key.KeyId);
        Assert.NotNull(key.Kty);
        Assert.NotNull(key.Alg);
    }
}
