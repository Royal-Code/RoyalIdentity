using System.Collections.Concurrent;
using RoyalIdentity.Options;
using RoyalIdentity.Users;
using RoyalIdentity.Users.Contracts;
using RoyalIdentity.Users.Defaults;
using RoyalIdentity.Utils;
using Tests.Integration.Prepare;

namespace Tests.Integration.Users;

/// <summary>
/// Fase 4 (plan-users-edge-session.md) — unit tests for the in-memory edge facades: the single-place
/// lockout policy in <see cref="MemoryLocalUserAuthenticator"/>/<see cref="LockoutPolicy"/>, and the
/// <c>SubjectId</c> being stable and separate from the username (<see cref="MemorySubjectStore"/>).
/// </summary>
public class LocalUserAuthenticatorTests
{
    private const string Password = "correct-horse";

    private static UserDetails NewUser(
        string username, string subjectId, string? password = Password, bool active = true)
        => new()
        {
            SubjectId = subjectId,
            Username = username,
            DisplayName = username,
            IsActive = active,
            PasswordHash = password is null ? null : PasswordHash.Create(password)
        };

    private static (MemoryLocalUserAuthenticator auth, ControlledTimeProvider clock, AccountOptions options)
        Build(params UserDetails[] users)
    {
        var dict = new ConcurrentDictionary<string, UserDetails>();
        foreach (var u in users)
            dict[u.Username] = u;

        var options = new AccountOptions();
        var clock = new ControlledTimeProvider(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        var auth = new MemoryLocalUserAuthenticator(
            dict, options, new DefaultPasswordProtector(), new LockoutPolicy(options, clock));

        return (auth, clock, options);
    }

    [Fact]
    public async Task Authenticate_NotFound_ReturnsNotFound()
    {
        var (auth, _, _) = Build();

        var result = await auth.AuthenticateLocalAsync("ghost", Password);

        Assert.False(result.Success);
        Assert.Equal(AuthenticationFailureReason.NotFound, result.Reason);
    }

    [Fact]
    public async Task Authenticate_Inactive_ReturnsInactive()
    {
        var user = NewUser("alice", "sub-alice", active: false);
        var (auth, _, _) = Build(user);

        var result = await auth.AuthenticateLocalAsync("alice", Password);

        Assert.False(result.Success);
        Assert.Equal(AuthenticationFailureReason.Inactive, result.Reason);
    }

    [Fact]
    public async Task Authenticate_NoPasswordHash_ReturnsInvalidCredentials()
    {
        var user = NewUser("alice", "sub-alice", password: null);
        var (auth, _, _) = Build(user);

        var result = await auth.AuthenticateLocalAsync("alice", Password);

        Assert.False(result.Success);
        Assert.Equal(AuthenticationFailureReason.InvalidCredentials, result.Reason);
    }

    [Fact]
    public async Task Authenticate_WrongPassword_IncrementsFailureCounter()
    {
        var user = NewUser("alice", "sub-alice");
        var (auth, _, _) = Build(user);

        var result = await auth.AuthenticateLocalAsync("alice", "wrong");

        Assert.False(result.Success);
        Assert.Equal(AuthenticationFailureReason.InvalidCredentials, result.Reason);
        Assert.Equal(1, user.LoginAttemptsWithPasswordErrors);
    }

    [Fact]
    public async Task Authenticate_Success_ResetsFailureCounter_AndReturnsSubjectId()
    {
        var user = NewUser("alice", "sub-alice");
        var (auth, _, _) = Build(user);

        await auth.AuthenticateLocalAsync("alice", "wrong");
        Assert.Equal(1, user.LoginAttemptsWithPasswordErrors);

        var result = await auth.AuthenticateLocalAsync("alice", Password);

        Assert.True(result.Success);
        Assert.NotNull(result.Subject);
        Assert.Equal("sub-alice", result.Subject.SubjectId);
        Assert.Equal(0, user.LoginAttemptsWithPasswordErrors);
    }

    [Fact]
    public async Task Authenticate_LocksOut_AfterMaxFailedAttempts_ThenExpires()
    {
        var user = NewUser("alice", "sub-alice");
        var (auth, clock, options) = Build(user);

        // Reach the lockout threshold (default MaxFailedAccessAttempts = 3).
        for (var i = 0; i < options.PasswordOptions.MaxFailedAccessAttempts; i++)
            await auth.AuthenticateLocalAsync("alice", "wrong");

        // Even the correct password is now rejected as Blocked (proving lockout, not a bad password).
        var blocked = await auth.AuthenticateLocalAsync("alice", Password);
        Assert.False(blocked.Success);
        Assert.Equal(AuthenticationFailureReason.Blocked, blocked.Reason);

        // After the lockout window elapses, the correct password succeeds again.
        clock.Advance(TimeSpan.FromMinutes(options.PasswordOptions.AccountLockoutDurationMinutes + 1));
        var allowed = await auth.AuthenticateLocalAsync("alice", Password);
        Assert.True(allowed.Success);
        Assert.Equal("sub-alice", allowed.Subject!.SubjectId);
    }

    [Fact]
    public async Task SubjectId_IsSeparateFromUsername_AndStableAcrossUsernameChange()
    {
        var users = new ConcurrentDictionary<string, UserDetails>();
        var user = NewUser("alice", "sub-x");
        users[user.Username] = user;
        var subjects = new MemorySubjectStore(users);

        Assert.NotEqual(user.Username, user.SubjectId);
        Assert.NotNull(await subjects.FindBySubjectIdAsync("sub-x"));

        // Renaming the username must not change the subject id nor break the sub-based lookup.
        user.Username = "alice-renamed";

        Assert.Equal("sub-x", user.SubjectId);
        var found = await subjects.FindBySubjectIdAsync("sub-x");
        Assert.NotNull(found);
        Assert.Equal("sub-x", found.SubjectId);
    }
}
