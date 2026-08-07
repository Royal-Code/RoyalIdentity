using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using RoyalIdentity.Users;
using Tests.Integration.Prepare;

namespace Tests.Integration.Characterization;

/// <summary>
/// IdP regression over the integral persistent composition introduced by plan-data-test-migration Fase 4.
/// </summary>
public class UserAccountsPersistentRegressionTests : IClassFixture<PersistentStorageAppFactory>
{
    private readonly PersistentStorageAppFactory factory;

    public UserAccountsPersistentRegressionTests(PersistentStorageAppFactory factory)
    {
        this.factory = factory;
    }

    [Fact]
    public async Task Login_Profile_UsesModuleSeededSubject()
    {
        var client = factory.CreateClient();

        await client.LoginAsync(factory.Handles.Demo, factory.Handles.Alice);
        var response = await client.GetAsync($"{factory.Handles.Demo.Path}/test/account/profile");

        response.EnsureSuccessStatusCode();
        var subject = await response.Content.ReadFromJsonAsync<Dictionary<string, JsonElement>>();

        Assert.NotNull(subject);
        Assert.Equal(factory.Handles.Alice.SubjectId, subject!["subjectId"].GetString());
        Assert.Equal("Alice", subject["displayName"].GetString());
        Assert.True(subject["isActive"].GetBoolean());
    }

    [Fact]
    public async Task SessionPrincipal_RemainsMinimal_WithPersistentModule()
    {
        var client = factory.CreateClient();

        await client.LoginAsync(factory.Handles.Demo, factory.Handles.Alice);
        var response = await client.GetAsync($"{factory.Handles.Demo.Path}/test/account/principal");

        response.EnsureSuccessStatusCode();
        var claims = await response.Content.ReadFromJsonAsync<List<ClaimJson>>();

        Assert.NotNull(claims);
        Assert.Contains(
            claims!,
            c => c.Type == JwtRegisteredClaimNames.Sub && c.Value == factory.Handles.Alice.SubjectId);
        Assert.Contains(claims!, c => c.Type == JwtRegisteredClaimNames.Name && c.Value == "Alice");
        Assert.Contains(claims!, c => c.Type == JwtRegisteredClaimNames.Sid);
        Assert.DoesNotContain(claims!, c => c.Type == JwtRegisteredClaimNames.Email);
        Assert.DoesNotContain(claims!, c => c.Type == Jwt.ClaimTypes.Role);
    }

    [Fact]
    public async Task UserInfo_ProjectsModuleClaims_ByRequestedIdentityScopes()
    {
        var client = factory.CreateClient();

        await client.LoginAsync(factory.Handles.Demo, factory.Handles.Alice);
        var tokens = await client.GetTokensAsync(
            factory.Handles.Demo,
            factory.Handles.DemoClient,
            "openid profile email");

        var message = new HttpRequestMessage(
            HttpMethod.Get,
            Oidc.Routes.BuildUserInfoUrl(factory.Handles.Demo.Path));
        message.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", tokens.AccessToken);

        var response = await client.SendAsync(message);

        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadFromJsonAsync<Dictionary<string, JsonElement>>();

        Assert.NotNull(content);
        Assert.Equal(factory.Handles.Alice.SubjectId, content![JwtRegisteredClaimNames.Sub].GetString());
        Assert.Equal("Alice", content[JwtRegisteredClaimNames.Name].GetString());
        Assert.Equal("alice", content[Jwt.ClaimTypes.PreferredUserName].GetString());
        Assert.Equal("Alice@example.com", content[JwtRegisteredClaimNames.Email].GetString());
        Assert.DoesNotContain(Jwt.ClaimTypes.Role, content.Keys);
    }

    [Fact]
    public async Task Logout_EndsModuleBackedLoginSession()
    {
        var client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        await client.LoginAsync(factory.Handles.Demo, factory.Handles.Alice);
        var protectedBeforeLogout = await client.GetAsync(
            $"{factory.Handles.Demo.Path}/test/protected-resource");
        var logout = await client.LogoutAsync(factory.Handles.Demo);
        var protectedAfterLogout = await client.GetAsync(
            $"{factory.Handles.Demo.Path}/test/protected-resource");

        Assert.Equal(HttpStatusCode.OK, protectedBeforeLogout.StatusCode);
        Assert.Equal(HttpStatusCode.OK, logout.StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, protectedAfterLogout.StatusCode);
    }

    [Fact]
    public async Task Login_IsRealmScoped_WithPersistentModule()
    {
        var client = factory.CreateClient();

        var isolated = new TestSubjectHandle(
            $"subject-{CryptoRandom.CreateUniqueId(12)}",
            $"isolated-{CryptoRandom.CreateUniqueId(8)}",
            "isolated-password");
        await factory.SeedAccountAsync(factory.Handles.Demo, isolated);

        var response = await CharacterizationSeed.PostLoginAsync(
            client,
            isolated.Username,
            isolated.Password,
            realm: factory.Handles.Account.Path);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // Q9 (plan-users-accounts-sqlite-hardening.md, Fase 3) — originally expanded the opt-in regression beyond
    // the happy path. The persistent composition preserves that invalid credentials return the same generic
    // anti-enumeration message and create no session. Uses Bob (not Alice, untouched by the tests above) to avoid
    // polluting shared IClassFixture state with a mutated failed-attempt counter.
    [Fact]
    public async Task Login_WhenInvalidPassword_IsRejected_WithGenericMessage_AndNoSession()
    {
        var client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var response = await CharacterizationSeed.PostLoginAsync(
            client,
            factory.Handles.Bob.Username,
            "wrong-password",
            factory.Handles.Demo.Path);
        var protectedResource = await client.GetAsync(
            $"{factory.Handles.Demo.Path}/test/protected-resource");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains(nameof(LoginFlowErrorCode.InvalidCredentials), content); // generic (anti-enumeration)
        Assert.Equal(HttpStatusCode.Redirect, protectedResource.StatusCode); // no session created
    }

    private sealed record ClaimJson(string Type, string Value);
}
