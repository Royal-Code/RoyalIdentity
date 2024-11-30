using RoyalIdentity.Contracts.Models.Messages;

namespace RoyalIdentity.Users;

public interface ISignOutManager
{
    Task<Uri> SignOutAsync(LogoutMessage message, CancellationToken ct);

    Task<string?> CreateLogoutIdAsync(CancellationToken ct);
}