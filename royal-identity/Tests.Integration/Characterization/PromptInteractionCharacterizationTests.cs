using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using RoyalIdentity.Extensions;
using RoyalIdentity.Models;
using RoyalIdentity.Utils;
using System.Net;
using Tests.Integration.Prepare;

namespace Tests.Integration.Characterization;

/// <summary>
/// Fase 2 (plan-users-edge-session.md) — characterization of the "force interaction" rules at the
/// authorize endpoint: an already-authenticated user must still be sent to the login page when the
/// request asks for it (<c>prompt=login</c>), when the existing authentication is too old
/// (<c>max_age</c>), or when the client-specific SSO lifetime has expired.
/// </summary>
public class PromptInteractionCharacterizationTests : IClassFixture<ControlledTimeAppFactory>
{
    private static readonly DateTimeOffset BaseTime =
        new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private readonly ControlledTimeAppFactory factory;

    public PromptInteractionCharacterizationTests(ControlledTimeAppFactory factory)
    {
        this.factory = factory;
    }

    private static string BuildAuthorizeUrl(params (string key, string value)[] extra)
        => BuildAuthorizeUrlForClient("demo_client", extra);

    private static string BuildAuthorizeUrlForClient(string clientId, params (string key, string value)[] extra)
    {
        var url = Oidc.Routes.BuildAuthorizeUrl(MemoryStorage.DemoRealm.Path)
            .AddQueryString("client_id", clientId)
            .AddQueryString("response_type", "code")
            .AddQueryString("response_mode", "query")
            .AddQueryString("scope", "openid profile")
            .AddQueryString("redirect_uri", "http://localhost:5000/callback")
            .AddQueryString("code_challenge", "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")
            .AddQueryString("code_challenge_method", "S256");

        foreach (var (key, value) in extra)
            url = url.AddQueryString(key, value);

        return url;
    }

    [Fact]
    public async Task Authorize_WhenAuthenticated_WithoutPrompt_IssuesCodeWithoutInteraction()
    {
        factory.Clock.SetUtcNow(BaseTime);

        // baseline: a fresh, authenticated session goes straight to the callback with a code
        var storage = factory.Services.GetRequiredService<MemoryStorage>();
        var (username, password) = CharacterizationSeed.SeedUser(storage, MemoryStorage.DemoRealm);
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await CharacterizationSeed.PostLoginAsync(client, username, password);

        var response = await client.GetAsync(BuildAuthorizeUrl());

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location?.ToString() ?? "";
        Assert.Contains("code=", location);
        Assert.DoesNotContain("account/login", location);
    }

    [Fact]
    public async Task Authorize_WhenAuthenticated_WithPromptLogin_ForcesLogin()
    {
        factory.Clock.SetUtcNow(BaseTime);

        var storage = factory.Services.GetRequiredService<MemoryStorage>();
        var (username, password) = CharacterizationSeed.SeedUser(storage, MemoryStorage.DemoRealm);
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await CharacterizationSeed.PostLoginAsync(client, username, password);

        var response = await client.GetAsync(BuildAuthorizeUrl(("prompt", "login")));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location?.ToString() ?? "";
        Assert.Contains("account/login", location);
        Assert.DoesNotContain("code=", location);
    }

    [Fact]
    public async Task Authorize_WhenAuthenticated_WithMaxAgeZero_ForcesLogin()
    {
        factory.Clock.SetUtcNow(BaseTime);

        var storage = factory.Services.GetRequiredService<MemoryStorage>();
        var (username, password) = CharacterizationSeed.SeedUser(storage, MemoryStorage.DemoRealm);
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await CharacterizationSeed.PostLoginAsync(client, username, password);

        // max_age=0 means "authentication must be effectively now"; one second later is too old.
        factory.Clock.Advance(TimeSpan.FromSeconds(1));
        var response = await client.GetAsync(BuildAuthorizeUrl(("max_age", "0")));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location?.ToString() ?? "";
        Assert.Contains("account/login", location);
        Assert.DoesNotContain("code=", location);
    }

    [Fact]
    public async Task Authorize_WhenClientUserSsoLifetimeExpires_ForcesLogin()
    {
        factory.Clock.SetUtcNow(BaseTime);

        var storage = factory.Services.GetRequiredService<MemoryStorage>();
        var (username, password) = CharacterizationSeed.SeedUser(storage, MemoryStorage.DemoRealm);

        var clientId = $"sso-client-{CryptoRandom.CreateUniqueId(6)}";
        storage.GetDemoRealmStore().Clients[clientId] = new Client
        {
            Realm = MemoryStorage.DemoRealm,
            Id = clientId,
            Name = "User SSO Lifetime Client",
            RequireClientSecret = false,
            RequirePkce = false,
            UserSsoLifetime = 60,
            AllowedGrantTypes = ["authorization_code"],
            AllowedIdentityScopes = { "openid", "profile" },
            AllowedResponseTypes = { "code" },
            RedirectUris = { "http://localhost:5000/**" }
        };

        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await CharacterizationSeed.PostLoginAsync(client, username, password);

        var freshResponse = await client.GetAsync(BuildAuthorizeUrlForClient(clientId));

        Assert.Equal(HttpStatusCode.Redirect, freshResponse.StatusCode);
        var freshLocation = freshResponse.Headers.Location?.ToString() ?? "";
        Assert.Contains("code=", freshLocation);
        Assert.DoesNotContain("account/login", freshLocation);

        factory.Clock.Advance(TimeSpan.FromSeconds(61));

        var expiredResponse = await client.GetAsync(BuildAuthorizeUrlForClient(clientId));

        Assert.Equal(HttpStatusCode.Redirect, expiredResponse.StatusCode);
        var expiredLocation = expiredResponse.Headers.Location?.ToString() ?? "";
        Assert.Contains("account/login", expiredLocation);
        Assert.DoesNotContain("code=", expiredLocation);
    }
}
