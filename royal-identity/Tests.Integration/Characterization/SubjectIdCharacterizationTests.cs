using Tests.Integration.Prepare;

namespace Tests.Integration.Characterization;

/// <summary>
/// Fase 4 (plan-users-edge-session.md) — evidence that the emitted <c>sub</c> is now the stable
/// <see cref="MemoryStorage.AliceSubjectId"/> (≠ username). This locks in the SubjectId flip end-to-end
/// through the live login + token path.
/// </summary>
public class SubjectIdCharacterizationTests : IClassFixture<AppFactory>
{
    private readonly AppFactory factory;

    public SubjectIdCharacterizationTests(AppFactory factory)
    {
        this.factory = factory;
    }

    [Fact]
    public async Task Login_IdToken_Sub_IsStableSubjectId_NotUsername()
    {
        var client = factory.CreateClient();
        await client.LoginAliceAsync();

        var tokens = await client.GetTokensAsync("demo_client", "openid profile");
        Assert.NotNull(tokens.IdentityToken);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(tokens.IdentityToken);
        var sub = jwt.Claims.First(c => c.Type == JwtRegisteredClaimNames.Sub).Value;

        Assert.Equal(MemoryStorage.AliceSubjectId, sub);
        Assert.NotEqual("alice", sub);
    }
}
