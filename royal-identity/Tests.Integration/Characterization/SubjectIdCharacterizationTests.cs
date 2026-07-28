using Tests.Integration.Prepare;

namespace Tests.Integration.Characterization;

/// <summary>
/// Fase 4 (plan-users-edge-session.md) — evidence that the emitted <c>sub</c> is now the stable
/// seeded subject identifier (different from the username). This locks in the SubjectId flip end-to-end
/// through the live login + token path.
/// </summary>
public class SubjectIdCharacterizationTests : IClassFixture<PersistentStorageAppFactory>
{
    private readonly PersistentStorageAppFactory factory;

    public SubjectIdCharacterizationTests(PersistentStorageAppFactory factory)
    {
        this.factory = factory;
    }

    [Fact]
    public async Task Login_IdToken_Sub_IsStableSubjectId_NotUsername()
    {
        var client = factory.CreateClient();
        await client.LoginAsync(factory.Handles.Demo, factory.Handles.Alice);

        var tokens = await client.GetTokensAsync(
            factory.Handles.Demo,
            factory.Handles.DemoClient,
            "openid profile");
        Assert.NotNull(tokens.IdentityToken);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(tokens.IdentityToken);
        var sub = jwt.Claims.First(c => c.Type == JwtRegisteredClaimNames.Sub).Value;

        Assert.Equal(factory.Handles.Alice.SubjectId, sub);
        Assert.NotEqual(factory.Handles.Alice.Username, sub);
    }
}
