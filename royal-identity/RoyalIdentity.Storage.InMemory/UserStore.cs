using Microsoft.Extensions.Options;
using RoyalIdentity.Options;
using RoyalIdentity.Users;
using RoyalIdentity.Users.Contracts;
using RoyalIdentity.Users.Defaults;

namespace RoyalIdentity.Storage.InMemory;

public class UserStore : IUserStore, IUserDetailsStore
{
    private readonly MemoryStorage storage;
    private readonly IOptions<AccountOptions> accountOptions;
    private readonly IUserSessionStore userSessionStore;
    private readonly IPasswordProtector passwordProtector;

    public UserStore(
        MemoryStorage storage,
        IOptions<AccountOptions> accountOptions,
        IUserSessionStore userSessionStore,
        IPasswordProtector passwordProtector)
    {
        this.storage = storage;
        this.accountOptions = accountOptions;
        this.userSessionStore = userSessionStore;
        this.passwordProtector = passwordProtector;
    }

    public Task<IdentityUser?> GetUserAsync(string userName, CancellationToken ct = default)
    {
        IdentityUser? user = null;
        if (storage.UsersDetails.TryGetValue(userName, out var userDetails))
            user = new DefaultIdentityUser(userDetails, accountOptions, userSessionStore, this, passwordProtector);
        return Task.FromResult(user);
    }

    public Task<bool> IsUserActive(string userName, CancellationToken ct = default)
    {
        storage.UsersDetails.TryGetValue(userName, out var userDetails);
        return Task.FromResult(userDetails?.IsActive ?? false);
    }

    public Task<UserDetails?> GetUserDetailsAsync(string userName, CancellationToken ct = default)
    {
        storage.UsersDetails.TryGetValue(userName, out var userDetails);
        return Task.FromResult(userDetails);
    }

    public Task SaveUserDetailsAsync(UserDetails details, CancellationToken ct = default)
    {
        storage.UsersDetails.AddOrUpdate(details.Username, details, (_, _) => details);
        return Task.CompletedTask;
    }
}
