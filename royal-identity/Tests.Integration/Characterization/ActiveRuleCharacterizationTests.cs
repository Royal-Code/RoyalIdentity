using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using RoyalIdentity.Contracts.Storage;
using RoyalIdentity.Extensions;
using System.Net;
using System.Net.Http.Json;
using Tests.Integration.Prepare;

namespace Tests.Integration.Characterization;

/// <summary>
/// Fase 6 (plan-users-edge-session.md) — the unified "active" rule across the three paths: a session-bound
/// principal whose session is absent is rejected at the token endpoint; an ended session re-prompts at
/// authorize; and an inactive account yields no profile claims at userinfo. (The cookie path is covered by
/// <see cref="UserSessionCharacterizationTests.Cookie_WhenSessionEnded_IsRejected"/>.)
/// </summary>
public class ActiveRuleCharacterizationTests : IClassFixture<AppFactory>
{
    private readonly AppFactory factory;

    public ActiveRuleCharacterizationTests(AppFactory factory)
    {
        this.factory = factory;
    }

    [Fact]
    public async Task TokenEndpoint_AuthCode_WhenSessionAbsent_IsRejected()
    {
        var storage = factory.Services.GetRequiredService<IStorage>();
        var resources = await storage.GetResourceStore(MemoryStorage.DemoRealm)
            .FindResourcesByScopeAsync(["openid", "profile"], default);

        // Principal for the active account 'alice' but with a sid that has NO backing session.
        var code = new RoyalIdentity.Models.Tokens.AuthorizationCode(
            "demo_client",
            SubjectFactory.Create(MemoryStorage.AliceSubjectId, "Alice", "admin"),
            "session",
            DateTime.UtcNow,
            300,
            resources,
            "http://localhost:5000/callback");
        await storage.GetAuthorizationCodeStore(MemoryStorage.DemoRealm).StoreAuthorizationCodeAsync(code, default);

        var client = factory.CreateClient();
        var response = await client.PostAsync(
            Oidc.Routes.BuildTokenUrl(MemoryStorage.DemoRealm.Path),
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "authorization_code",
                ["code"] = code.Code,
                ["client_id"] = "demo_client",
                ["redirect_uri"] = "http://localhost:5000/callback",
            }));

        // account is active, but the session is absent ⇒ not active ⇒ invalid_grant
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Authorize_WhenSessionEnded_RePromptsLogin()
    {
        var storage = factory.Services.GetRequiredService<MemoryStorage>();
        var sessionStorage = factory.Services.GetRequiredService<IStorage>();
        var (username, password) = CharacterizationSeed.SeedUser(storage, MemoryStorage.DemoRealm);
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        await CharacterizationSeed.PostLoginAsync(client, username, password);
        var session = CharacterizationSeed.FindSession(storage, MemoryStorage.DemoRealm, username);
        Assert.NotNull(session);
        await sessionStorage.GetUserSessionStore(MemoryStorage.DemoRealm).EndAsync(session.Id, default);

        var authorizeUrl = Oidc.Routes.BuildAuthorizeUrl(MemoryStorage.DemoRealm.Path)
            .AddQueryString("client_id", "demo_client")
            .AddQueryString("response_type", "code")
            .AddQueryString("response_mode", "query")
            .AddQueryString("scope", "openid profile")
            .AddQueryString("redirect_uri", "http://localhost:5000/callback")
            .AddQueryString("code_challenge", "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")
            .AddQueryString("code_challenge_method", "S256");

        var response = await client.GetAsync(authorizeUrl);

        // the cookie's session is no longer valid ⇒ authorize re-prompts login instead of issuing a code
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location?.ToString() ?? "";
        Assert.Contains("account/login", location);
        Assert.DoesNotContain("code=", location);
    }

    [Fact]
    public async Task UserInfo_WhenAccountInactive_ReturnsNoProfileClaims()
    {
        var storage = factory.Services.GetRequiredService<MemoryStorage>();
        var (username, password) = CharacterizationSeed.SeedUser(storage, MemoryStorage.DemoRealm);
        var client = factory.CreateClient();

        await CharacterizationSeed.PostLoginAsync(client, username, password);
        var tokens = await client.GetTokensAsync("demo_client", "openid profile");

        // deactivate the account after the token was issued
        CharacterizationSeed.GetDetails(storage, MemoryStorage.DemoRealm, username).IsActive = false;

        var message = new HttpRequestMessage(HttpMethod.Get, Oidc.Routes.BuildUserInfoUrl(MemoryStorage.DemoRealm.Path));
        message.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", tokens.AccessToken);
        var response = await client.SendAsync(message);

        Assert.True(response.IsSuccessStatusCode);
        var content = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        Assert.NotNull(content);
        Assert.Contains("sub", content);          // sub is always returned
        Assert.DoesNotContain("name", content);   // profile claims withheld for an inactive account
    }
}
