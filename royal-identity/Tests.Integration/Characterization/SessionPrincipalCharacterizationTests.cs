using System.Net.Http.Json;
using Tests.Integration.Prepare;

namespace Tests.Integration.Characterization;

/// <summary>
/// Fase 7 (plan-users-edge-session.md) — end-to-end check that the cookie (session) principal written at
/// sign-in carries only the minimal protocol claims; roles and profile claims (e.g. email) are NOT in the
/// cookie. They still reach tokens/userinfo via IProfileService (covered elsewhere).
/// </summary>
public class SessionPrincipalCharacterizationTests : IClassFixture<PersistentStorageAppFactory>
{
    private readonly PersistentStorageAppFactory factory;

    public SessionPrincipalCharacterizationTests(PersistentStorageAppFactory factory)
    {
        this.factory = factory;
    }

    private sealed record ClaimDto(string type, string value);

    [Fact]
    public async Task CookiePrincipal_AfterLogin_CarriesOnlyMinimalClaims()
    {
        // alice is seeded with role=admin + email claims; none of those must end up in the cookie principal.
        var client = factory.CreateClient();
        await client.LoginAsync(factory.Handles.Demo, factory.Handles.Alice);

        var response = await client.GetAsync(
            $"{factory.Handles.Demo.Path}/test/account/principal");
        response.EnsureSuccessStatusCode();
        var claims = await response.Content.ReadFromJsonAsync<ClaimDto[]>();

        Assert.NotNull(claims);
        var types = claims.Select(c => c.type).ToHashSet();

        Assert.Contains(JwtRegisteredClaimNames.Sub, types);
        Assert.Contains(JwtRegisteredClaimNames.Name, types);
        Assert.Contains(JwtRegisteredClaimNames.Sid, types);
        Assert.Contains(JwtRegisteredClaimNames.AuthTime, types);
        Assert.Contains(JwtRegisteredClaimNames.Amr, types);
        Assert.Contains(Jwt.ClaimTypes.IdentityProvider, types);

        // sub is the stable SubjectId, not the username
        Assert.Equal(
            factory.Handles.Alice.SubjectId,
            claims.First(c => c.type == JwtRegisteredClaimNames.Sub).value);

        // roles / profile claims do NOT leak into the cookie principal
        Assert.DoesNotContain(Jwt.ClaimTypes.Role, types);
        Assert.DoesNotContain("email", types);
    }
}
