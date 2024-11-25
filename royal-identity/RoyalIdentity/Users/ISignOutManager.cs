namespace RoyalIdentity.Users;

public interface ISignOutManager
{
    Task<Uri> SignOutAsync(string sessionId, string? postLogoutRedirectUri, string? state, CancellationToken ct);

    Task<string?> CreateLogoutIdAsync(CancellationToken ct);
}