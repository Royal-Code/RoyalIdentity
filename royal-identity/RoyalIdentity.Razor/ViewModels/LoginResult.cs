namespace RoyalIdentity.Razor.ViewModels;

public record LoginResult(
    LoginResultType Type,
    string? NavigateTo = null,
    string? ErrorMessage = null,
    bool ForceLoad = false
);

public enum LoginResultType
{
    Error,
    RequiresConsent,
    SignedInPage,
    Success
}
