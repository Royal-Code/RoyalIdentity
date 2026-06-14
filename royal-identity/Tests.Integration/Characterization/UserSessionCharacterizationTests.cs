using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using RoyalIdentity.Contracts.Storage;
using RoyalIdentity.Extensions;
using System.Net;
using Tests.Integration.Prepare;

namespace Tests.Integration.Characterization;

/// <summary>
/// Fase 2 (plan-users-edge-session.md) — characterization tests of the CURRENT login/session/"active"
/// behavior at the HTTP level. They form the safety net for the borda+sessão refactor: each must stay
/// green at the end of every later phase. They assert behavior, not internal types, so they survive the
/// internal redesign. Where a behavior is already covered elsewhere (LoginPageTests, LoginConsentUIFlowTests,
/// EndSessionTests, RealmIsolationTests) this only complements it.
/// </summary>
public class UserSessionCharacterizationTests : IClassFixture<AppFactory>
{
    private readonly AppFactory factory;

    public UserSessionCharacterizationTests(AppFactory factory)
    {
        this.factory = factory;
    }

    private MemoryStorage Storage => factory.Services.GetRequiredService<MemoryStorage>();

    // ─── Login creates an active, realm-scoped session ────────────────────────

    [Fact]
    public async Task Login_WhenValid_CreatesActiveRealmScopedSession()
    {
        var storage = Storage;
        var (username, password) = CharacterizationSeed.SeedUser(storage, MemoryStorage.DemoRealm);
        var client = factory.CreateClient();

        var response = await CharacterizationSeed.PostLoginAsync(client, username, password);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var session = CharacterizationSeed.FindSession(storage, MemoryStorage.DemoRealm, username);
        Assert.NotNull(session);
        Assert.True(session.IsActive);
        Assert.Equal(Oidc.AuthMethods.Password, session.AuthenticationMethod);

        // realm-scoped: the session exists only in the realm it was created in
        Assert.Null(CharacterizationSeed.FindSession(storage, MemoryStorage.ServerRealm, username));
    }

    // ─── Failed login: no session, failure counter increments ─────────────────

    [Fact]
    public async Task Login_WhenInvalidPassword_DoesNotCreateSession_AndIncrementsFailureCounter()
    {
        var storage = Storage;
        var (username, _) = CharacterizationSeed.SeedUser(storage, MemoryStorage.DemoRealm);
        var client = factory.CreateClient();

        var response = await CharacterizationSeed.PostLoginAsync(client, username, "wrong-password");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null(CharacterizationSeed.FindSession(storage, MemoryStorage.DemoRealm, username));
        Assert.Equal(1, CharacterizationSeed.GetDetails(storage, MemoryStorage.DemoRealm, username).LoginAttemptsWithPasswordErrors);
    }

    [Fact]
    public async Task Login_WhenSuccessAfterFailures_ResetsFailureCounter()
    {
        var storage = Storage;
        var (username, password) = CharacterizationSeed.SeedUser(storage, MemoryStorage.DemoRealm);
        var client = factory.CreateClient();

        // two failures (below the lockout threshold of 3)
        await CharacterizationSeed.PostLoginAsync(client, username, "wrong-1");
        await CharacterizationSeed.PostLoginAsync(client, username, "wrong-2");
        Assert.Equal(2, CharacterizationSeed.GetDetails(storage, MemoryStorage.DemoRealm, username).LoginAttemptsWithPasswordErrors);

        var response = await CharacterizationSeed.PostLoginAsync(client, username, password);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(0, CharacterizationSeed.GetDetails(storage, MemoryStorage.DemoRealm, username).LoginAttemptsWithPasswordErrors);
        Assert.NotNull(CharacterizationSeed.FindSession(storage, MemoryStorage.DemoRealm, username));
    }

    // ─── Inactive / blocked accounts: generic message, no session ─────────────

    [Fact]
    public async Task Login_WhenUserInactive_IsRejected_WithGenericMessage_AndNoSession()
    {
        var storage = Storage;
        var (username, password) = CharacterizationSeed.SeedUser(storage, MemoryStorage.DemoRealm, active: false);
        var client = factory.CreateClient();

        var response = await CharacterizationSeed.PostLoginAsync(client, username, password);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("Invalid username or password", content); // generic (anti-enumeration)
        Assert.Null(CharacterizationSeed.FindSession(storage, MemoryStorage.DemoRealm, username));
    }

    [Fact]
    public async Task Login_WhenLockedOut_IsRejected_AfterMaxFailedAttempts_WithGenericMessage()
    {
        var storage = Storage;
        var (username, password) = CharacterizationSeed.SeedUser(storage, MemoryStorage.DemoRealm);
        var client = factory.CreateClient();

        // hit the lockout threshold (MaxFailedAccessAttempts = 3)
        await CharacterizationSeed.PostLoginAsync(client, username, "wrong-1");
        await CharacterizationSeed.PostLoginAsync(client, username, "wrong-2");
        await CharacterizationSeed.PostLoginAsync(client, username, "wrong-3");

        // now even the CORRECT password is rejected — proving lockout, not a bad password
        var response = await CharacterizationSeed.PostLoginAsync(client, username, password);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("Invalid username or password", content); // same generic message as invalid creds
        Assert.Null(CharacterizationSeed.FindSession(storage, MemoryStorage.DemoRealm, username));
    }

    // ─── Cookie validation against the session store ──────────────────────────

    [Fact]
    public async Task Cookie_WhenSessionEnded_IsRejected_OnNextRequest()
    {
        var storage = Storage;
        var sessionStorage = factory.Services.GetRequiredService<IStorage>();
        var (username, password) = CharacterizationSeed.SeedUser(storage, MemoryStorage.DemoRealm);
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        await CharacterizationSeed.PostLoginAsync(client, username, password);

        // while the session is active, the cookie authenticates the protected endpoint
        var authorized = await client.GetAsync("demo/test/account/profile");
        Assert.Equal(HttpStatusCode.OK, authorized.StatusCode);

        // end the session server-side; the cookie is now backed by an inactive session
        var session = CharacterizationSeed.FindSession(storage, MemoryStorage.DemoRealm, username);
        Assert.NotNull(session);
        await sessionStorage.GetUserSessionStore(MemoryStorage.DemoRealm).EndAsync(session.Id, default);

        // OnValidatePrincipal rejects the principal and the protected endpoint challenges to login
        var rejected = await client.GetAsync("demo/test/account/profile");
        Assert.Equal(HttpStatusCode.Redirect, rejected.StatusCode);
        Assert.Contains("account/login", rejected.Headers.Location?.ToString() ?? "");
    }

    // ─── Code issuance records the client on the session ──────────────────────

    [Fact]
    public async Task CodeIssuance_RecordsRequestingClient_OnSession()
    {
        var storage = Storage;
        var (username, password) = CharacterizationSeed.SeedUser(storage, MemoryStorage.DemoRealm);
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        await CharacterizationSeed.PostLoginAsync(client, username, password);
        var code = await client.GetAuthorizeAsync(); // demo_client

        Assert.NotNull(code);
        var session = CharacterizationSeed.FindSession(storage, MemoryStorage.DemoRealm, username);
        Assert.NotNull(session);
        Assert.Contains(session.Clients, c => c.ClientId == "demo_client");
    }

    [Fact]
    public async Task CodeIssuance_SameClientTwice_RecordedOnce()
    {
        var storage = Storage;
        var (username, password) = CharacterizationSeed.SeedUser(storage, MemoryStorage.DemoRealm);
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        await CharacterizationSeed.PostLoginAsync(client, username, password);
        Assert.NotNull(await client.GetAuthorizeAsync());
        Assert.NotNull(await client.GetAuthorizeAsync()); // same client again

        var session = CharacterizationSeed.FindSession(storage, MemoryStorage.DemoRealm, username);
        Assert.NotNull(session);
        Assert.Single(session.Clients, c => c.ClientId == "demo_client"); // deduplicated
    }

    // ─── Logout ends the session ──────────────────────────────────────────────

    [Fact]
    public async Task Logout_EndsTheSession()
    {
        var storage = Storage;
        var (username, password) = CharacterizationSeed.SeedUser(storage, MemoryStorage.DemoRealm);
        var client = factory.CreateClient();

        await CharacterizationSeed.PostLoginAsync(client, username, password);
        var session = CharacterizationSeed.FindSession(storage, MemoryStorage.DemoRealm, username);
        Assert.NotNull(session);
        Assert.True(session.IsActive);

        await client.LogoutAsync();

        Assert.False(session.IsActive);
    }
}
