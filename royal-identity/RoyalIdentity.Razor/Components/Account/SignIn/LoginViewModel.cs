namespace RoyalIdentity.Razor.Components.Account.SignIn;

public class LoginViewModel : LoginInputModel
{
    private ExternalProvider[]? visibleExternalProviders = null;


    public bool AllowRememberLogin { get; set; } = true;

    public bool EnableLocalLogin { get; set; } = true;

    public bool EnableExternalLogin => GetVisibleExternalProviders().Length is not 0;

    public ExternalProvider[] ExternalProviders { get; set; } = [];

    public ExternalProvider[] GetVisibleExternalProviders()
        => visibleExternalProviders ??= ExternalProviders.Where(static x => !string.IsNullOrWhiteSpace(x.DisplayName)).ToArray();


    public bool IsExternalLoginOnly => !EnableLocalLogin && ExternalProviders?.Count() == 1;

    public string? ExternalLoginScheme => IsExternalLoginOnly
        ? ExternalProviders?.SingleOrDefault()?.AuthenticationScheme
        : null;
}
