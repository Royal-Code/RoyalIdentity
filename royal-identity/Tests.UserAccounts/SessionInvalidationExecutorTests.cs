using RoyalIdentity.UserAccounts.Integration;
using RoyalIdentity.UserAccounts.Options;
using RoyalIdentity.Users;
using RoyalIdentity.Users.Contracts;

namespace Tests.UserAccounts;

/// <summary>
/// Fase 8 (plan-users-security-lifecycle.md) — the integration bridges the module's per-trigger invalidation policy
/// (<see cref="SessionInvalidation"/>, Q7) to the IdP's core <see cref="SessionRevocation"/> scope (Q13). These cover
/// the translation; the actual revocation behavior is tested against the core service.
/// </summary>
public class SessionInvalidationExecutorTests
{
    private sealed class RecordingRevocation : ISessionRevocationService
    {
        public int Calls { get; private set; }

        public string? SubjectId { get; private set; }

        public SessionRevocation Revocation { get; private set; }

        public string? CurrentSessionId { get; private set; }

        public Task RevokeAsync(
            string subjectId, SessionRevocation revocation, string? currentSessionId, CancellationToken ct = default)
        {
            Calls++;
            SubjectId = subjectId;
            Revocation = revocation;
            CurrentSessionId = currentSessionId;
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task Execute_TranslatesAllSessionsAndRefreshTokens()
    {
        var revocation = new RecordingRevocation();
        var executor = new SessionInvalidationExecutor(revocation);

        await executor.ExecuteAsync(
            "s1",
            SessionInvalidation.AllSessions | SessionInvalidation.RefreshTokens,
            "current");

        Assert.Equal("s1", revocation.SubjectId);
        Assert.Equal("current", revocation.CurrentSessionId);
        Assert.Equal(
            SessionRevocation.CurrentSession | SessionRevocation.OtherSessions | SessionRevocation.RefreshTokens,
            revocation.Revocation);
    }

    [Fact]
    public async Task Execute_TranslatesOtherSessionsOnly()
    {
        var revocation = new RecordingRevocation();
        var executor = new SessionInvalidationExecutor(revocation);

        await executor.ExecuteAsync("s1", SessionInvalidation.OtherSessions, "current");

        Assert.Equal(SessionRevocation.OtherSessions, revocation.Revocation);
    }

    [Fact]
    public async Task Execute_None_DelegatesAsNoOpScope()
    {
        var revocation = new RecordingRevocation();
        var executor = new SessionInvalidationExecutor(revocation);

        await executor.ExecuteAsync("s1", SessionInvalidation.None, null);

        Assert.Equal(1, revocation.Calls);
        Assert.Equal(SessionRevocation.None, revocation.Revocation);
    }
}
