using RoyalIdentity.Contexts.Items;

namespace Tests.Identity.Contexts;

public class TokenTests
{
    [Fact]
    public void Constructor_StoresOnlyAnObfuscatedTokenValue()
    {
        const string rawToken = "sensitive-token-value";

        var token = new Token("access_token", rawToken);

        Assert.Equal("access_token", token.TokenType);
        Assert.Equal("sens****alue", token.TokenValue);
        Assert.DoesNotContain(rawToken, token.TokenValue, StringComparison.Ordinal);
    }
}
