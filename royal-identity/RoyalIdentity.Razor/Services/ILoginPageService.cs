using RoyalIdentity.Razor.ViewModels;

namespace RoyalIdentity.Razor.Services;

public interface ILoginPageService
{
    Task<LoginViewModel?> BuildViewModelAsync(string? returnUrl, CancellationToken ct);

    Task<LoginResult> LoginAsync(LoginInputModel input, CancellationToken ct);
}
