using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using RoyalIdentity.Extensions;
using System.Net;
using Tests.Integration.Prepare;

namespace Tests.Integration.Characterization;

/// <summary>
/// Fase 2 (plan-users-edge-session.md) — characterization of the "force interaction" rules at the
/// authorize endpoint: an already-authenticated user must still be sent to the login page when the
/// request asks for it (<c>prompt=login</c>) or when the existing authentication is too old
/// (<c>max_age</c>). The same <see cref="RoyalIdentity.Contexts.Decorators.PromptLoginDecorator"/>
/// path also backs <c>Client.UserSsoLifetime</c>.
/// </summary>
public class PromptInteractionCharacterizationTests : IClassFixture<AppFactory>
{
    private readonly AppFactory factory;

    public PromptInteractionCharacterizationTests(AppFactory factory)
    {
        this.factory = factory;
    }

    private static string BuildAuthorizeUrl(params (string key, string value)[] extra)
    {
        var url = Oidc.Routes.BuildAuthorizeUrl(MemoryStorage.DemoRealm.Path)
            .AddQueryString("client_id", "demo_client")
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
        var storage = factory.Services.GetRequiredService<MemoryStorage>();
        var (username, password) = CharacterizationSeed.SeedUser(storage, MemoryStorage.DemoRealm);
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await CharacterizationSeed.PostLoginAsync(client, username, password);

        // max_age=0 means "authentication must be effectively now" — the just-created session is too old
        var response = await client.GetAsync(BuildAuthorizeUrl(("max_age", "0")));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location?.ToString() ?? "";
        Assert.Contains("account/login", location);
        Assert.DoesNotContain("code=", location);
    }
}
