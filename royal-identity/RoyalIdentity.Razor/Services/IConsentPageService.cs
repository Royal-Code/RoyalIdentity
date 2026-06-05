using RoyalIdentity.Razor.ViewModels;

namespace RoyalIdentity.Razor.Services;

public interface IConsentPageService
{
    Task<ConsentViewModel?> BuildViewModelAsync(string? returnUrl, CancellationToken ct);

    Task<ConsentResult> ProcessConsentAsync(string realm, ConsentInputModel input, CancellationToken ct);
}
