using RoyalIdentity.Users;
using RoyalIdentity.Users.Contracts;

namespace RoyalIdentity.Storage.InMemory;

public class UserStore : IUserStore
{
    private readonly MemoryStorage storage;

    public UserStore(MemoryStorage storage)
    {
        this.storage = storage;
    }

    public Task<IdentityUser?> GetUserAsync(string userName, CancellationToken ct = default)
    {
        storage.Users.TryGetValue(userName, out var user);
        return Task.FromResult(user);
    }

    public Task<bool> IsUserActive(string userName, CancellationToken ct = default)
    {
        storage.Users.TryGetValue(userName, out var user);
        return Task.FromResult(user is not null);
    }
}
