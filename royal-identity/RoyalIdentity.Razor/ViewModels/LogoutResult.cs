namespace RoyalIdentity.Razor.ViewModels;

public record LogoutResult(
    LogoutResultType Type,
    string? NavigateTo = null
);

public enum LogoutResultType
{
    RequiresConfirmation,
    LoggedOut,
    Error
}
