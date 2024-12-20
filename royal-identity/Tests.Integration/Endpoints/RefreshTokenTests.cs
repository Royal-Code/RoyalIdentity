using Microsoft.Extensions.DependencyInjection;
using RoyalIdentity.Contracts.Storage;
using Tests.Integration.Prepare;

namespace Tests.Integration.Endpoints;

public class RefreshTokenTests : IClassFixture<AppFactory>
{
    private readonly AppFactory factory;

    public RefreshTokenTests(AppFactory factory)
    {
        this.factory = factory;
    }

    [Fact]
    public async Task Post_WhenValidRefreshToken_ShouldReturnNewTokens()
    {
        // Arrange

        // get services
        var refreshStore = factory.Services.GetRequiredService<IRefreshTokenStore>();
        var resourcesStore = factory.Services.GetRequiredService<IResourceStore>();


        // create a refresh token and store it
        //var refreshToken = new RoyalIdentity.Models.Tokens.RefreshToken(
            
        //    );
    }
}
