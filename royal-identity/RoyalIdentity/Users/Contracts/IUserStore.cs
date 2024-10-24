namespace RoyalIdentity.Users.Contracts;

public interface IUserStore
{
    Task<IdentityUser> GetUserAsync(string userName, CancellationToken ct = default);

    Task<bool> IsUserActive(string userName, CancellationToken ct = default);
}
