using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Tests.Integration.Prepare;

namespace Tests.Integration.Characterization;

/// <summary>
/// Fase 10 (plan-users-accounts-module-v2): IdP regression over the opt-in UserAccounts integration.
/// The default <see cref="AppFactory"/> suite continues to run against the in-memory fake.
/// </summary>
public class UserAccountsOptInRegressionTests : IClassFixture<UserAccountsAppFactory>
{
	private readonly UserAccountsAppFactory factory;

	public UserAccountsOptInRegressionTests(UserAccountsAppFactory factory)
	{
		this.factory = factory;
	}

	[Fact]
	public async Task Login_Profile_UsesModuleSeededSubject()
	{
		var client = factory.CreateClient();

		await client.LoginAliceAsync();
		var response = await client.GetAsync("demo/test/account/profile");

		response.EnsureSuccessStatusCode();
		var subject = await response.Content.ReadFromJsonAsync<Dictionary<string, JsonElement>>();

		Assert.NotNull(subject);
		Assert.Equal(MemoryStorage.AliceSubjectId, subject!["subjectId"].GetString());
		Assert.Equal("Alice", subject["displayName"].GetString());
		Assert.True(subject["isActive"].GetBoolean());
	}

	[Fact]
	public async Task SessionPrincipal_RemainsMinimal_WithModuleOptIn()
	{
		var client = factory.CreateClient();

		await client.LoginAliceAsync();
		var response = await client.GetAsync("demo/test/account/principal");

		response.EnsureSuccessStatusCode();
		var claims = await response.Content.ReadFromJsonAsync<List<ClaimJson>>();

		Assert.NotNull(claims);
		Assert.Contains(claims!, c => c.Type == JwtRegisteredClaimNames.Sub && c.Value == MemoryStorage.AliceSubjectId);
		Assert.Contains(claims!, c => c.Type == JwtRegisteredClaimNames.Name && c.Value == "Alice");
		Assert.Contains(claims!, c => c.Type == JwtRegisteredClaimNames.Sid);
		Assert.DoesNotContain(claims!, c => c.Type == JwtRegisteredClaimNames.Email);
		Assert.DoesNotContain(claims!, c => c.Type == Jwt.ClaimTypes.Role);
	}

	[Fact]
	public async Task UserInfo_ProjectsModuleClaims_ByRequestedIdentityScopes()
	{
		var client = factory.CreateClient();

		await client.LoginAliceAsync();
		var tokens = await client.GetTokensAsync("demo_client", "openid profile email");

		var message = new HttpRequestMessage(HttpMethod.Get, Oidc.Routes.BuildUserInfoUrl(MemoryStorage.DemoRealm.Path));
		message.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", tokens.AccessToken);

		var response = await client.SendAsync(message);

		response.EnsureSuccessStatusCode();
		var content = await response.Content.ReadFromJsonAsync<Dictionary<string, JsonElement>>();

		Assert.NotNull(content);
		Assert.Equal(MemoryStorage.AliceSubjectId, content![JwtRegisteredClaimNames.Sub].GetString());
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

		await client.LoginAliceAsync();
		var protectedBeforeLogout = await client.GetAsync("demo/test/protected-resource");
		var logout = await client.LogoutAsync();
		var protectedAfterLogout = await client.GetAsync("demo/test/protected-resource");

		Assert.Equal(HttpStatusCode.OK, protectedBeforeLogout.StatusCode);
		Assert.Equal(HttpStatusCode.OK, logout.StatusCode);
		Assert.Equal(HttpStatusCode.Redirect, protectedAfterLogout.StatusCode);
	}

	[Fact]
	public async Task Login_IsRealmScoped_WithModuleOptIn()
	{
		var client = factory.CreateClient();

		var response = await CharacterizationSeed.PostLoginAsync(client, "alice", "alice", realm: "account");

		Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
	}

	// Q9 (plan-users-accounts-sqlite-hardening.md, Fase 3) — expands the opt-in regression beyond the happy
	// path: an invalid password against the module must be rejected with the same generic (anti-enumeration)
	// message as the fake, and must not create a session. Uses Bob (not Alice, untouched by the tests above)
	// to avoid polluting shared IClassFixture state with a mutated failed-attempt counter.
	[Fact]
	public async Task Login_WhenInvalidPassword_IsRejected_WithGenericMessage_AndNoSession()
	{
		var client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
		{
			AllowAutoRedirect = false
		});

		var response = await CharacterizationSeed.PostLoginAsync(client, "bob", "wrong-password");
		var protectedResource = await client.GetAsync("demo/test/protected-resource");

		Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
		var content = await response.Content.ReadAsStringAsync();
		Assert.Contains("Invalid username or password", content); // generic (anti-enumeration)
		Assert.Equal(HttpStatusCode.Redirect, protectedResource.StatusCode); // no session created
	}

	private sealed record ClaimJson(string Type, string Value);
}
