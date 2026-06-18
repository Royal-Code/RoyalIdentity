using System.Net.Http.Json;
using Tests.Integration.Prepare;

namespace Tests.Integration.Characterization;

/// <summary>
/// Fase 8 (plan-users-edge-session.md) — the claims seam: <c>IProfileService.GetProfileDataAsync</c> sources
/// the issued claims from <c>IUserClaimsProvider</c> passing only primitives (identity scope names + claim
/// types), returning <c>Claim</c> directly. These tests lock in that id_token and userinfo carry the
/// claims projected by the requested identity scopes, and that a property whose type is NOT requested by any
/// identity scope (alice's <c>role</c>) is filtered out by the seam — it never leaks into tokens.
/// (Inactive accounts projecting no claims is covered by
/// <see cref="ActiveRuleCharacterizationTests.UserInfo_WhenAccountInactive_ReturnsNoProfileClaims"/>.)
/// </summary>
public class ClaimsSeamCharacterizationTests : IClassFixture<AppFactory>
{
    private readonly AppFactory factory;

    public ClaimsSeamCharacterizationTests(AppFactory factory)
    {
        this.factory = factory;
    }

    [Fact]
    public async Task UserInfo_ProjectsClaimsByIdentityScope_AndFiltersUnrequested()
    {
        var client = factory.CreateClient();
        await client.LoginAliceAsync();
        var tokens = await client.GetTokensAsync("demo_client", "openid profile email");

        var message = new HttpRequestMessage(HttpMethod.Get, Oidc.Routes.BuildUserInfoUrl(MemoryStorage.DemoRealm.Path));
        message.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", tokens.AccessToken);
        var response = await client.SendAsync(message);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        Assert.NotNull(content);

        // claims projected by the requested identity scopes (profile → name/preferred_username; email → email)
        Assert.Contains(JwtRegisteredClaimNames.Sub, content);
        Assert.Contains(JwtRegisteredClaimNames.Name, content);
        Assert.Contains(Jwt.ClaimTypes.PreferredUserName, content);
        Assert.Contains(JwtRegisteredClaimNames.Email, content);

        // alice has a 'role' claim in her record, but no identity scope requests it ⇒ the seam filters it out
        Assert.DoesNotContain(Jwt.ClaimTypes.Role, content);
    }

    [Fact]
    public async Task IdToken_CarriesProfileClaimsBySeam_AndFiltersUnrequested()
    {
        var client = factory.CreateClient();
        await client.LoginAliceAsync();
        var tokens = await client.GetTokensAsync("demo_client", "openid profile email");
        Assert.NotNull(tokens.IdentityToken);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(tokens.IdentityToken);
        var types = jwt.Claims.Select(c => c.Type).ToHashSet();

        Assert.Contains(JwtRegisteredClaimNames.Name, types);
        Assert.Contains(Jwt.ClaimTypes.PreferredUserName, types);
        Assert.Contains(JwtRegisteredClaimNames.Email, types);

        // unrequested property (role) does not leak into the id_token via the seam
        Assert.DoesNotContain(Jwt.ClaimTypes.Role, types);
    }

    [Fact]
    public async Task AccessToken_ApiOnly_DoesNotLeakProfileClaims()
    {
        var client = factory.CreateClient();
        await client.LoginAliceAsync();
        var tokens = await client.GetTokensAsync("demo_consent_client", "api");
        Assert.NotNull(tokens.AccessToken);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(tokens.AccessToken);
        var types = jwt.Claims.Select(c => c.Type).ToHashSet();

        Assert.Contains(JwtRegisteredClaimNames.Sub, types);
        Assert.Contains(Jwt.ClaimTypes.Scope, types);

        // No identity scope was requested, so the seam must not project profile properties into the API token.
        Assert.DoesNotContain(JwtRegisteredClaimNames.Name, types);
        Assert.DoesNotContain(Jwt.ClaimTypes.PreferredUserName, types);
        Assert.DoesNotContain(JwtRegisteredClaimNames.Email, types);
        Assert.DoesNotContain(Jwt.ClaimTypes.Role, types);
    }
}
