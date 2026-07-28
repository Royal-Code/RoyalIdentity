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
public class UserSessionCharacterizationTests : IClassFixture<PersistentStorageAppFactory>
{
    private readonly PersistentStorageAppFactory factory;

    public UserSessionCharacterizationTests(PersistentStorageAppFactory factory)
    {
        this.factory = factory;
    }

    // ─── Login creates an active, realm-scoped session ────────────────────────

    [Fact]
    public async Task Login_WhenValid_CreatesActiveRealmScopedSession()
    {
        var subject = await CharacterizationSeed.SeedUserAsync(factory, factory.Handles.Demo);
        var client = factory.CreateClient();

        var response = await CharacterizationSeed.PostLoginAsync(
            client,
            subject.Username,
            subject.Password,
            factory.Handles.Demo.Path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var session = await CharacterizationSeed.FindSessionAsync(
            factory,
            factory.Handles.Demo,
            subject);
        Assert.NotNull(session);
        Assert.True(session.IsActive);
        Assert.Equal(Oidc.AuthMethods.Password, session.AuthenticationMethod);

        // realm-scoped: the session exists only in the realm it was created in
        Assert.Empty(await factory.FindSessionsAsync(factory.Handles.Server, subject));
    }

    // ─── Failed login: no session, failure counter increments ─────────────────

    [Fact]
    public async Task Login_WhenInvalidPassword_DoesNotCreateSession_AndIncrementsFailureCounter()
    {
        var subject = await CharacterizationSeed.SeedUserAsync(factory, factory.Handles.Demo);
        var client = factory.CreateClient();

        var response = await CharacterizationSeed.PostLoginAsync(
            client,
            subject.Username,
            "wrong-password",
            factory.Handles.Demo.Path);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(await factory.FindSessionsAsync(factory.Handles.Demo, subject));
        var state = await factory.FindAccountStateAsync(factory.Handles.Demo, subject);
        Assert.NotNull(state);
        Assert.Equal(1, state.FailedPasswordAttempts);
    }

    [Fact]
    public async Task Login_WhenSuccessAfterFailures_ResetsFailureCounter()
    {
        var subject = await CharacterizationSeed.SeedUserAsync(factory, factory.Handles.Demo);
        var client = factory.CreateClient();

        // two failures (below the lockout threshold of 3)
        await CharacterizationSeed.PostLoginAsync(
            client, subject.Username, "wrong-1", factory.Handles.Demo.Path);
        await CharacterizationSeed.PostLoginAsync(
            client, subject.Username, "wrong-2", factory.Handles.Demo.Path);
        var failedState = await factory.FindAccountStateAsync(factory.Handles.Demo, subject);
        Assert.NotNull(failedState);
        Assert.Equal(2, failedState.FailedPasswordAttempts);

        var response = await CharacterizationSeed.PostLoginAsync(
            client, subject.Username, subject.Password, factory.Handles.Demo.Path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var successfulState = await factory.FindAccountStateAsync(factory.Handles.Demo, subject);
        Assert.NotNull(successfulState);
        Assert.Equal(0, successfulState.FailedPasswordAttempts);
        Assert.NotNull(await CharacterizationSeed.FindSessionAsync(
            factory,
            factory.Handles.Demo,
            subject));
    }

    // ─── Inactive / blocked accounts: generic message, no session ─────────────

    [Fact]
    public async Task Login_WhenUserInactive_IsRejected_WithGenericMessage_AndNoSession()
    {
        var subject = await CharacterizationSeed.SeedUserAsync(
            factory,
            factory.Handles.Demo,
            active: false);
        var client = factory.CreateClient();

        var response = await CharacterizationSeed.PostLoginAsync(
            client,
            subject.Username,
            subject.Password,
            factory.Handles.Demo.Path);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("Invalid username or password", content); // generic (anti-enumeration)
        Assert.Empty(await factory.FindSessionsAsync(factory.Handles.Demo, subject));
    }

    [Fact]
    public async Task Login_WhenLockedOut_IsRejected_AfterMaxFailedAttempts_WithGenericMessage()
    {
        var subject = await CharacterizationSeed.SeedUserAsync(factory, factory.Handles.Demo);
        var client = factory.CreateClient();

        // hit the lockout threshold (MaxFailedAccessAttempts = 3)
        await CharacterizationSeed.PostLoginAsync(
            client, subject.Username, "wrong-1", factory.Handles.Demo.Path);
        await CharacterizationSeed.PostLoginAsync(
            client, subject.Username, "wrong-2", factory.Handles.Demo.Path);
        await CharacterizationSeed.PostLoginAsync(
            client, subject.Username, "wrong-3", factory.Handles.Demo.Path);

        // now even the CORRECT password is rejected — proving lockout, not a bad password
        var response = await CharacterizationSeed.PostLoginAsync(
            client,
            subject.Username,
            subject.Password,
            factory.Handles.Demo.Path);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("Invalid username or password", content); // same generic message as invalid creds
        Assert.Empty(await factory.FindSessionsAsync(factory.Handles.Demo, subject));
        var state = await factory.FindAccountStateAsync(factory.Handles.Demo, subject);
        Assert.NotNull(state);
        Assert.NotNull(state.LockoutEndAt);
    }

    // ─── Cookie validation against the session store ──────────────────────────

    [Fact]
    public async Task Cookie_WhenSessionEnded_IsRejected_OnNextRequest()
    {
        var subject = await CharacterizationSeed.SeedUserAsync(factory, factory.Handles.Demo);
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        await CharacterizationSeed.PostLoginAsync(
            client,
            subject.Username,
            subject.Password,
            factory.Handles.Demo.Path);

        // while the session is active, the cookie authenticates the protected endpoint
        var authorized = await client.GetAsync($"{factory.Handles.Demo.Path}/test/account/profile");
        Assert.Equal(HttpStatusCode.OK, authorized.StatusCode);

        // end the session server-side; the cookie is now backed by an inactive session
        var session = await CharacterizationSeed.FindSessionAsync(
            factory,
            factory.Handles.Demo,
            subject);
        Assert.NotNull(session);
        await EndSessionAsync(session.Id);

        // OnValidatePrincipal rejects the principal and the protected endpoint challenges to login
        var rejected = await client.GetAsync($"{factory.Handles.Demo.Path}/test/account/profile");
        Assert.Equal(HttpStatusCode.Redirect, rejected.StatusCode);
        Assert.Contains("account/login", rejected.Headers.Location?.ToString() ?? "");
    }

    // ─── Code issuance records the client on the session ──────────────────────

    [Fact]
    public async Task CodeIssuance_RecordsRequestingClient_OnSession()
    {
        var subject = await CharacterizationSeed.SeedUserAsync(factory, factory.Handles.Demo);
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        await CharacterizationSeed.PostLoginAsync(
            client,
            subject.Username,
            subject.Password,
            factory.Handles.Demo.Path);
        var code = await client.GetAuthorizeAsync(
            factory.Handles.Demo,
            factory.Handles.DemoClient);

        Assert.NotNull(code);
        var session = await CharacterizationSeed.FindSessionAsync(
            factory,
            factory.Handles.Demo,
            subject);
        Assert.NotNull(session);
        Assert.Contains(factory.Handles.DemoClient.ClientId, session.ClientIds);
    }

    [Fact]
    public async Task CodeIssuance_SameClientTwice_RecordedOnce()
    {
        var subject = await CharacterizationSeed.SeedUserAsync(factory, factory.Handles.Demo);
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        await CharacterizationSeed.PostLoginAsync(
            client,
            subject.Username,
            subject.Password,
            factory.Handles.Demo.Path);
        Assert.NotNull(await client.GetAuthorizeAsync(
            factory.Handles.Demo,
            factory.Handles.DemoClient));
        Assert.NotNull(await client.GetAuthorizeAsync(
            factory.Handles.Demo,
            factory.Handles.DemoClient));

        var session = await CharacterizationSeed.FindSessionAsync(
            factory,
            factory.Handles.Demo,
            subject);
        Assert.NotNull(session);
        Assert.Single(
            session.ClientIds,
            clientId => clientId == factory.Handles.DemoClient.ClientId);
    }

    // ─── Logout ends the session ──────────────────────────────────────────────

    [Fact]
    public async Task Logout_EndsTheSession()
    {
        var subject = await CharacterizationSeed.SeedUserAsync(factory, factory.Handles.Demo);
        var client = factory.CreateClient();

        await CharacterizationSeed.PostLoginAsync(
            client,
            subject.Username,
            subject.Password,
            factory.Handles.Demo.Path);
        var session = await CharacterizationSeed.FindSessionAsync(
            factory,
            factory.Handles.Demo,
            subject);
        Assert.NotNull(session);
        Assert.True(session.IsActive);

        await client.LogoutAsync(factory.Handles.Demo);

        var ended = await CharacterizationSeed.FindSessionAsync(
            factory,
            factory.Handles.Demo,
            subject);
        Assert.NotNull(ended);
        Assert.False(ended.IsActive);
    }

    private async Task EndSessionAsync(string sessionId)
    {
        using var scope = factory.Services.CreateScope();
        var storage = scope.ServiceProvider.GetRequiredService<IStorage>();
        var realm = await factory.LoadRealmAsync(factory.Handles.Demo);
        await storage.GetUserSessionStore(realm).EndAsync(sessionId, default);
    }
}
