using RoyalIdentity.Users.Defaults;

namespace RoyalIdentity.Users.Contracts;

public interface IUserDetailsStore
{
    Task<UserDetails?> GetUserDetailsAsync(string userName, CancellationToken ct = default);

    Task SaveUserDetailsAsync(UserDetails details, CancellationToken ct = default);
}