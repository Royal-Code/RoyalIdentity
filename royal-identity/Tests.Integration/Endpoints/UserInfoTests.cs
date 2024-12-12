using Tests.Integration.Prepare;

namespace Tests.Integration.Endpoints;

public class UserInfoTests : IClassFixture<AppFactory>
{
    private readonly AppFactory factory;

    public UserInfoTests(AppFactory factory)
    {
        this.factory = factory;
    }

    [Fact]
    public async Task Get_Must_ReturnTheUserInfo()
    {
        // Arrange
        var client = factory.CreateClient();
        await client.LoginAliceAsync();


    }
}
