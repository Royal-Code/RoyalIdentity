namespace RoyalIdentity.Razor.ViewModels;

public record ConsentResult(
    ConsentResultType Type,
    string? NavigateTo = null,
    string? ErrorMessage = null,
    bool ForceLoad = false
);

public enum ConsentResultType
{
    Granted,
    Denied,
    ValidationError
}
