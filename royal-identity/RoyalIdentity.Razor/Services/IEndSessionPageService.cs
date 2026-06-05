using RoyalIdentity.Razor.ViewModels;

namespace RoyalIdentity.Razor.Services;

public interface IEndSessionPageService
{
    Task<LogoutResult> BeginLogoutAsync(string? logoutId, CancellationToken ct);

    Task<LogoutResult> ConfirmLogoutAsync(string confirmedId, CancellationToken ct);

    Task<LoggingOutViewModel?> BuildLoggingOutAsync(string? logoutId, CancellationToken ct);
}
