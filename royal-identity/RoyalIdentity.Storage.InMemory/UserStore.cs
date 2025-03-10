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

    public Task<IdentityUser?> GetUserAsync(string userName, CancellationToken ct = default)
    {
        IdentityUser? user = null;
        if (usersDetails.TryGetValue(userName, out var userDetails))
            user = new DefaultIdentityUser(userDetails, accountOptions, userSessionStore, this, passwordProtector, clock);
        return Task.FromResult(user);
    }

    public Task<bool> IsUserActive(string userName, CancellationToken ct = default)
    {
        usersDetails.TryGetValue(userName, out var userDetails);
        return Task.FromResult(userDetails?.IsActive ?? false);
    }

    public Task<UserDetails?> GetUserDetailsAsync(string userName, CancellationToken ct = default)
    {
        usersDetails.TryGetValue(userName, out var userDetails);
        return Task.FromResult(userDetails);
    }

    public Task SaveUserDetailsAsync(UserDetails details, CancellationToken ct = default)
    {
        usersDetails.AddOrUpdate(details.Username, details, (_, _) => details);
        return Task.CompletedTask;
    }
}
