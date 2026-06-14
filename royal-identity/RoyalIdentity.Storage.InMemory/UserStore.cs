using RoyalIdentity.Options;
using RoyalIdentity.Users;
using RoyalIdentity.Users.Contracts;
using RoyalIdentity.Users.Defaults;
using System.Collections.Concurrent;

namespace RoyalIdentity.Storage.InMemory;

public class UserStore : IUserStore, IUserDetailsStore
{
    private readonly ConcurrentDictionary<string, UserDetails> usersDetails;
    private readonly AccountOptions accountOptions;
    private readonly IUserSessionStore userSessionStore;
    private readonly IPasswordProtector passwordProtector;
    private readonly TimeProvider clock;

    public UserStore(
        ConcurrentDictionary<string, UserDetails> usersDetails,
        AccountOptions accountOptions,
        IUserSessionStore userSessionStore,
        IPasswordProtector passwordProtector,
        TimeProvider clock)
    {
        this.usersDetails = usersDetails;
        this.accountOptions = accountOptions;
        this.userSessionStore = userSessionStore;
        this.passwordProtector = passwordProtector;
        this.clock = clock;
    }

    public Task<IdentityUser?> GetUserAsync(string usernameOrSubjectId, CancellationToken ct = default)
    {
        var userDetails = Resolve(usernameOrSubjectId);
        IdentityUser? user = userDetails is null
            ? null
            : new DefaultIdentityUser(userDetails, accountOptions, userSessionStore, this, passwordProtector, clock);
        return Task.FromResult(user);
    }

    public Task<bool> IsUserActive(string usernameOrSubjectId, CancellationToken ct = default)
    {
        var userDetails = Resolve(usernameOrSubjectId);
        return Task.FromResult(userDetails?.IsActive ?? false);
    }

    public Task<UserDetails?> GetUserDetailsAsync(string subjectIdOrUsername, CancellationToken ct = default)
    {
        return Task.FromResult(Resolve(subjectIdOrUsername));
    }

    /// <summary>
    /// Resolves a user by username (the dictionary key) or by <see cref="UserDetails.SubjectId"/>. The two
    /// key spaces never collide (subject ids are opaque GUIDs), so a single key works for both the login
    /// path (passes username) and the profile/session path (passes the <c>sub</c>). The fake store scans
    /// for the subject id; a real store (UsersAccounts module) would keep an index.
    /// </summary>
    private UserDetails? Resolve(string key)
    {
        if (usersDetails.TryGetValue(key, out var byUsername))
            return byUsername;

        return usersDetails.Values.FirstOrDefault(u => u.SubjectId == key);
    }

    public Task SaveUserDetailsAsync(UserDetails details, CancellationToken ct = default)
    {
        usersDetails.AddOrUpdate(details.Username, details, (_, _) => details);
        return Task.CompletedTask;
    }
}
