using System.Security.Claims;
using RoyalIdentity.Contracts;
using RoyalIdentity.Users;
using RoyalIdentity.Users.Contracts;
using RoyalIdentity.Users.Defaults;
using Tests.Integration.Prepare;

namespace Tests.Integration.Users;

/// <summary>
/// Fase 5 (plan-users-edge-session.md) — unit tests for the pure session store + <c>IUserSessionService</c>:
/// session is created (not as a side-effect), holds the <c>SubjectId</c> (not the user), "session valid"
/// means present-and-active (absent ⇒ invalid), and clients are deduplicated. The realm is resolved by an
/// accessor, never a method parameter.
/// </summary>
public class DefaultUserSessionServiceTests
{
    private sealed class FakeRealmAccessor(RoyalIdentity.Models.Realm realm) : ICurrentRealmAccessor
    {
        public RoyalIdentity.Models.Realm GetCurrentRealm() => realm;

        public bool TryGetCurrentRealm(out RoyalIdentity.Models.Realm? r)
        {
            r = realm;
            return true;
        }
    }

    /// <summary>
    /// Minimal <see cref="IUserDirectory"/> for these baseline tests: the security-state capability is absent (the
    /// DemoRealm does not enable state invalidation), and the other ports are not exercised.
    /// </summary>
    private sealed class StubUserDirectory : IUserDirectory
    {
        public ISubjectStore GetSubjectStore(RoyalIdentity.Models.Realm realm) => throw new NotSupportedException();

        public ILocalUserAuthenticator GetLocalAuthenticator(RoyalIdentity.Models.Realm realm) => throw new NotSupportedException();

        public IUserClaimsProvider GetClaimsProvider(RoyalIdentity.Models.Realm realm) => throw new NotSupportedException();

        public IUserSecurityStateProvider? GetSecurityStateProvider(RoyalIdentity.Models.Realm realm) => null;
    }

    private static (DefaultUserSessionService service, ControlledTimeProvider clock) Build()
    {
        var clock = new ControlledTimeProvider(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        var storage = new MemoryStorage(clock);
        var accessor = new FakeRealmAccessor(MemoryStorage.DemoRealm);
        return (new DefaultUserSessionService(storage, accessor, new StubUserDirectory(), clock), clock);
    }

    private static ClaimsPrincipal PrincipalWithSid(string? sid)
    {
        var claims = sid is null ? [] : new[] { new Claim(JwtRegisteredClaimNames.Sid, sid) };
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
    }

    [Fact]
    public async Task StartAsync_CreatesActiveSession_HoldingSubjectId()
    {
        var (service, _) = Build();

        var session = await service.StartAsync(new Subject("sub-1", "Display", true), "pwd", "local");

        Assert.False(string.IsNullOrEmpty(session.Id));
        Assert.Equal("sub-1", session.SubjectId);
        Assert.Equal("pwd", session.AuthenticationMethod);
        Assert.Equal("local", session.IdentityProvider);
        Assert.True(session.IsActive);
    }

    [Fact]
    public async Task StartAsync_CapturesSecurityStamp_WhenProvided()
    {
        var (service, _) = Build();

        var session = await service.StartAsync(
            new Subject("sub-1", "Display", true),
            "pwd",
            "local",
            "stamp-1");

        Assert.Equal("stamp-1", session.SecurityStamp);
    }

    [Fact]
    public async Task IsSessionValid_Active_True_Absent_False_Ended_False()
    {
        var (service, _) = Build();
        var session = await service.StartAsync(new Subject("sub-1", "Display", true), "pwd", "local");

        Assert.True(await service.IsSessionValidAsync(PrincipalWithSid(session.Id)));
        Assert.False(await service.IsSessionValidAsync(PrincipalWithSid("missing-sid")));
        Assert.False(await service.IsSessionValidAsync(PrincipalWithSid(null)));

        await service.EndAsync(session.Id);
        Assert.False(await service.IsSessionValidAsync(PrincipalWithSid(session.Id)));
    }

    [Fact]
    public async Task GetCurrentAsync_ResolvesSessionFromPrincipalSid()
    {
        var (service, _) = Build();
        var session = await service.StartAsync(new Subject("sub-1", "Display", true), "pwd", "local");

        var current = await service.GetCurrentAsync(PrincipalWithSid(session.Id));
        Assert.NotNull(current);
        Assert.Equal(session.Id, current.Id);

        Assert.Null(await service.GetCurrentAsync(PrincipalWithSid(null)));
    }

    [Fact]
    public async Task RecordClientAsync_DeduplicatesByClientId()
    {
        var (service, _) = Build();
        var session = await service.StartAsync(new Subject("sub-1", "Display", true), "pwd", "local");

        await service.RecordClientAsync(session.Id, "client-a");
        await service.RecordClientAsync(session.Id, "client-a"); // duplicate
        await service.RecordClientAsync(session.Id, "client-b");

        var current = await service.GetCurrentAsync(PrincipalWithSid(session.Id));
        Assert.NotNull(current);
        Assert.Equal(2, current.Clients.Count);
        Assert.Single(current.Clients, c => c.ClientId == "client-a");
        Assert.Single(current.Clients, c => c.ClientId == "client-b");
    }
}
