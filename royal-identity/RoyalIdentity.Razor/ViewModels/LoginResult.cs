using RoyalIdentity.Razor.Localization;

namespace RoyalIdentity.Razor.ViewModels;

/// <summary>
/// Outcome of a login attempt as the page renders it. Failure carries a
/// <see cref="AccountUiMessageCode"/>, resolved to text only at render time in the request's culture (DF11).
/// </summary>
public record LoginResult(
    LoginResultType Type,
    string? NavigateTo = null,
    AccountUiMessageCode? MessageCode = null,
    bool ForceLoad = false
);

public enum LoginResultType
{
    Error,
    RequiresConsent,
    SignedInPage,
    Success
}
