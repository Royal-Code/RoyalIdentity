using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;
using RoyalIdentity.Contracts;
using RoyalIdentity.Contracts.Storage;
using RoyalIdentity.Options;
using RoyalIdentity.Users;
using RoyalIdentity.Users.Contracts;
using RoyalIdentity.Users.Defaults;
using RoyalIdentity.Utils;
using Tests.Integration.Prepare;
using RealmModel = RoyalIdentity.Models.Realm;

namespace Tests.Integration.Users;

/// <summary>
/// Fase 8 (plan-users-security-lifecycle.md) — SSO session lifetime (Realm-only, ADR-017 §2.12), idle touch with
/// write throttle, passive invalidation by <c>SessionsValidAfter</c> (Q7/Q15), and active revocation by subject
/// (Q13). Session lifecycle policy is IdP-owned (<c>RealmOptions.Session</c>); the account security marker is read
/// through the optional <see cref="IUserSecurityStateProvider"/> capability.
/// </summary>
public class SessionLifecycleTests
{
    private static readonly DateTimeOffset T0 = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    // ---- SSO session expiration (Realm-only) ----

    [Fact]
    public async Task SsoExpiration_MaxOnly_InvalidatesAfterMax()
    {
        var realm = RealmWith(s =>
        {
            s.EnableSsoSessionExpiration = true;
            s.SsoSessionMaxMinutes = 30;
            s.SsoSessionIdleMinutes = 0;
        });
        var (service, clock, _) = Build(realm);

        var session = await service.StartAsync(new Subject("s1", "Display", true), "pwd", "local");
        Assert.Equal(T0.UtcDateTime.AddMinutes(30), session.ExpiresAt);

        clock.SetUtcNow(T0.AddMinutes(29));
        Assert.True(await service.IsSessionValidAsync(Principal(session.Id)));

        clock.SetUtcNow(T0.AddMinutes(30));
        Assert.False(await service.IsSessionValidAsync(Principal(session.Id)));
    }

    // ---- Idle touch with throttle ----

    [Fact]
    public async Task IdleTouch_Throttles_Writes_AdvancesExpiry_AndCapsAtMax()
    {
        var realm = RealmWith(s =>
        {
            s.EnableSsoSessionExpiration = true;
            s.SsoSessionMaxMinutes = 60;
            s.SsoSessionIdleMinutes = 10;
            s.IdleTouchIntervalMinutes = 2;
        });
        var (service, clock, storage) = Build(realm);

        var session = await service.StartAsync(new Subject("s1", "Display", true), "pwd", "local");
        // Idle pulls the initial expiry to StartedAt + idle.
        Assert.Equal(T0.UtcDateTime.AddMinutes(10), session.ExpiresAt);

        // Within the throttle window: valid, but no write (LastSeenAt/ExpiresAt unchanged).
        clock.SetUtcNow(T0.AddMinutes(1));
        Assert.True(await service.IsSessionValidAsync(Principal(session.Id)));
        var afterThrottle = storage.Sessions[session.Id];
        Assert.Equal(T0.UtcDateTime, afterThrottle.LastSeenAt);
        Assert.Equal(T0.UtcDateTime.AddMinutes(10), afterThrottle.ExpiresAt);

        // Past the throttle window: the touch advances LastSeenAt and ExpiresAt = now + idle.
        clock.SetUtcNow(T0.AddMinutes(3));
        Assert.True(await service.IsSessionValidAsync(Principal(session.Id)));
        var afterTouch = storage.Sessions[session.Id];
        Assert.Equal(T0.UtcDateTime.AddMinutes(3), afterTouch.LastSeenAt);
        Assert.Equal(T0.UtcDateTime.AddMinutes(13), afterTouch.ExpiresAt);

        // Idle expiry catches a session left untouched past its idle window.
        clock.SetUtcNow(T0.AddMinutes(14));
        Assert.False(await service.IsSessionValidAsync(Principal(session.Id)));
    }

    [Fact]
    public async Task IdleTouch_NeverExtendsBeyondMax()
    {
        var realm = RealmWith(s =>
        {
            s.EnableSsoSessionExpiration = true;
            s.SsoSessionMaxMinutes = 20;
            s.SsoSessionIdleMinutes = 15;
            s.IdleTouchIntervalMinutes = 2;
        });
        var (service, clock, storage) = Build(realm);

        var session = await service.StartAsync(new Subject("s1", "Display", true), "pwd", "local");

        // A touch still within the idle window but close to max caps ExpiresAt at StartedAt + max (now + idle would
        // overshoot it).
        clock.SetUtcNow(T0.AddMinutes(14));
        Assert.True(await service.IsSessionValidAsync(Principal(session.Id)));
        Assert.Equal(T0.UtcDateTime.AddMinutes(20), storage.Sessions[session.Id].ExpiresAt);
    }

    // ---- Passive invalidation by SessionsValidAfter (Q7/Q15) ----

    [Fact]
    public async Task SessionsValidAfter_InvalidatesSessionStartedBeforeMarker_WhenPolicyOn()
    {
        var realm = RealmWith(s => s.EnableSessionInvalidationByState = true);
        var provider = new StubSecurityStateProvider(new UserSecurityState("stamp", T0.AddMinutes(5)));
        var (service, _, _) = Build(realm, provider);

        // StartedAt = T0; marker = T0 + 5 ⇒ started before ⇒ invalid.
        var session = await service.StartAsync(new Subject("s1", "Display", true), "pwd", "local");
        Assert.False(await service.IsSessionValidAsync(Principal(session.Id)));
    }

    [Fact]
    public async Task SessionsValidAfter_AllowsSessionStartedAtOrAfterMarker()
    {
        var realm = RealmWith(s => s.EnableSessionInvalidationByState = true);
        // Boundary: marker == StartedAt ⇒ valid (StartedAt >= marker).
        var provider = new StubSecurityStateProvider(new UserSecurityState("stamp", T0));
        var (service, _, _) = Build(realm, provider);

        var session = await service.StartAsync(new Subject("s1", "Display", true), "pwd", "local");
        Assert.True(await service.IsSessionValidAsync(Principal(session.Id)));
    }

    [Fact]
    public async Task SessionsValidAfter_NotEnforced_WhenPolicyOff()
    {
        var realm = RealmWith(_ => { }); // policy off
        var provider = new StubSecurityStateProvider(new UserSecurityState("stamp", T0.AddMinutes(5)));
        var (service, _, _) = Build(realm, provider);

        var session = await service.StartAsync(new Subject("s1", "Display", true), "pwd", "local");
        Assert.True(await service.IsSessionValidAsync(Principal(session.Id)));
    }

    [Fact]
    public async Task SessionsValidAfter_PolicyOn_WithoutCapability_IsConfigurationError()
    {
        var realm = RealmWith(s => s.EnableSessionInvalidationByState = true);
        var (service, _, _) = Build(realm, securityStateProvider: null);

        var session = await service.StartAsync(new Subject("s1", "Display", true), "pwd", "local");
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.IsSessionValidAsync(Principal(session.Id)));
    }

    // ---- Active revocation by subject (Q13) ----

    [Fact]
    public async Task Revoke_OtherSessions_PreservesCurrent()
    {
        var realm = RealmWith(_ => { });
        var (_, _, storage) = Build(realm);
        SeedSession(storage, "current", "s1");
        SeedSession(storage, "other", "s1");
        SeedSession(storage, "foreign", "s2");

        var revocation = new DefaultSessionRevocationService(storage, new FakeRealmAccessor(realm));
        await revocation.RevokeAsync("s1", SessionRevocation.OtherSessions, "current");

        Assert.True(storage.Sessions["current"].IsActive);
        Assert.False(storage.Sessions["other"].IsActive);
        Assert.True(storage.Sessions["foreign"].IsActive);
    }

    [Fact]
    public async Task Revoke_AllSessions_EndsAllOfSubject()
    {
        var realm = RealmWith(_ => { });
        var (_, _, storage) = Build(realm);
        SeedSession(storage, "current", "s1");
        SeedSession(storage, "other", "s1");
        SeedSession(storage, "foreign", "s2");

        var revocation = new DefaultSessionRevocationService(storage, new FakeRealmAccessor(realm));
        await revocation.RevokeAsync("s1", SessionRevocation.AllSessions, "current");

        Assert.False(storage.Sessions["current"].IsActive);
        Assert.False(storage.Sessions["other"].IsActive);
        Assert.True(storage.Sessions["foreign"].IsActive);
    }

    [Fact]
    public async Task Revoke_CurrentSessionOnly_EndsCurrent()
    {
        var realm = RealmWith(_ => { });
        var (_, _, storage) = Build(realm);
        SeedSession(storage, "current", "s1");
        SeedSession(storage, "other", "s1");

        var revocation = new DefaultSessionRevocationService(storage, new FakeRealmAccessor(realm));
        await revocation.RevokeAsync("s1", SessionRevocation.CurrentSession, "current");

        Assert.False(storage.Sessions["current"].IsActive);
        Assert.True(storage.Sessions["other"].IsActive);
    }

    [Fact]
    public async Task Revoke_CurrentSessionOnly_DoesNotEndForeignCurrentSessionId()
    {
        var realm = RealmWith(_ => { });
        var (_, _, storage) = Build(realm);
        SeedSession(storage, "owned", "s1");
        SeedSession(storage, "foreign", "s2");

        var revocation = new DefaultSessionRevocationService(storage, new FakeRealmAccessor(realm));
        await revocation.RevokeAsync("s1", SessionRevocation.CurrentSession, "foreign");

        Assert.True(storage.Sessions["owned"].IsActive);
        Assert.True(storage.Sessions["foreign"].IsActive);
    }

    [Fact]
    public async Task Revoke_RefreshTokens_RemovesBySubject()
    {
        var realm = RealmWith(_ => { });
        var (_, _, storage) = Build(realm);
        SeedRefreshToken(storage, "rt-s1-a", "s1");
        SeedRefreshToken(storage, "rt-s1-b", "s1");
        SeedRefreshToken(storage, "rt-s2", "s2");

        var revocation = new DefaultSessionRevocationService(storage, new FakeRealmAccessor(realm));
        await revocation.RevokeAsync("s1", SessionRevocation.RefreshTokens, null);

        Assert.False(storage.RefreshTokens.ContainsKey("rt-s1-a"));
        Assert.False(storage.RefreshTokens.ContainsKey("rt-s1-b"));
        Assert.True(storage.RefreshTokens.ContainsKey("rt-s2"));
    }

    // ---- harness ----

    private (DefaultUserSessionService service, ControlledTimeProvider clock, SessionStorageDouble storage) Build(
        RealmModel realm, IUserSecurityStateProvider? securityStateProvider = null)
    {
        var clock = new ControlledTimeProvider(T0);
        var storage = new SessionStorageDouble(clock);
        var accessor = new FakeRealmAccessor(realm);
        var directory = new StubUserDirectory(securityStateProvider);
        return (new DefaultUserSessionService(storage, accessor, directory, clock), clock, storage);
    }

    private static RealmModel RealmWith(Action<SessionOptions> configure)
    {
        var options = new RealmOptions(new ServerOptions());
        configure(options.Session);
        return new RealmModel("r-test", "test.com", "test", "Test", false, options);
    }

    private static ClaimsPrincipal Principal(string sid)
        => new(new ClaimsIdentity(new[] { new Claim(JwtRegisteredClaimNames.Sid, sid) }, "test"));

    private static void SeedSession(SessionStorageDouble storage, string sid, string subjectId)
    {
        storage.Sessions[sid] = new UserSession
        {
            Id = sid,
            SubjectId = subjectId,
            AuthenticationMethod = "pwd",
            IdentityProvider = "local",
            StartedAt = T0.UtcDateTime,
            LastSeenAt = T0.UtcDateTime,
        };
    }

    private static void SeedRefreshToken(SessionStorageDouble storage, string token, string subjectId)
    {
        // DF41: no identifier of a previous access token — the refresh token never depended on that row.
        storage.RefreshTokens[token] = new RoyalIdentity.Models.Tokens.RefreshToken(
            subjectId, "sid", [], "client", "issuer", T0.UtcDateTime, 3600, token);
    }

    private sealed class FakeRealmAccessor(RealmModel realm) : ICurrentRealmAccessor
    {
        public RealmModel GetCurrentRealm() => realm;

        public bool TryGetCurrentRealm([NotNullWhen(true)] out RealmModel? r)
        {
            r = realm;
            return true;
        }
    }

    private sealed class StubUserDirectory(IUserSecurityStateProvider? securityStateProvider) : IUserDirectory
    {
        public ISubjectStore GetSubjectStore(RealmModel realm) => throw new NotSupportedException();

        public ILocalUserAuthenticator GetLocalAuthenticator(RealmModel realm) => throw new NotSupportedException();

        public IUserClaimsProvider GetClaimsProvider(RealmModel realm) => throw new NotSupportedException();

        public IUserSecurityStateProvider? GetSecurityStateProvider(RealmModel realm) => securityStateProvider;
    }

    private sealed class StubSecurityStateProvider(UserSecurityState? state) : IUserSecurityStateProvider
    {
        public Task<UserSecurityState?> GetSecurityStateAsync(string subjectId, CancellationToken ct = default)
            => Task.FromResult(state);
    }

}
