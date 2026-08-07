using RoyalIdentity.Razor.Localization;

namespace RoyalIdentity.Razor.ViewModels;

/// <summary>
/// Outcome of a consent submission. Validation failures carry a <see cref="AccountUiMessageCode"/> rather than
/// an English phrase (DF11).
/// </summary>
public record ConsentResult(
    ConsentResultType Type,
    string? NavigateTo = null,
    AccountUiMessageCode? MessageCode = null,
    bool ForceLoad = false
);

public enum ConsentResultType
{
    Granted,
    Denied,
    ValidationError
}
