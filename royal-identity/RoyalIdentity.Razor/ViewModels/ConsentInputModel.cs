namespace RoyalIdentity.Razor.ViewModels;

/// <summary>
/// Values submitted by the consent screen buttons, carried in <see cref="ConsentInputModel.Button"/>.
/// </summary>
public static class ConsentButtons
{
    /// <summary>
    /// Indicates that the user granted consent.
    /// </summary>
    public const string Yes = "yes";

    /// <summary>
    /// Indicates that the user denied consent.
    /// </summary>
    public const string No = "no";
}

/// <summary>
/// Input posted by the consent screen.
/// </summary>
public class ConsentInputModel
{
    /// <summary>
    /// Gets or sets the identity scopes submitted by the user.
    /// </summary>
    public ICollection<ScopeConsentInputModel> IdentityScopesConsent { get; set; } = [];

    /// <summary>
    /// Gets or sets the API scopes submitted by the user.
    /// </summary>
    public ICollection<ScopeConsentInputModel> ApiScopesConsent { get; set; } = [];

    /// <summary>
    /// Gets or sets a value indicating whether the consent should be remembered.
    /// </summary>
    public bool RememberConsent { get; set; }

    /// <summary>
    /// Gets or sets the return URL that identifies the authorization request.
    /// </summary>
    public string ReturnUrl { get; set; }

    /// <summary>
    /// Gets or sets the optional consent description supplied by the user.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the consent button value submitted by the user.
    /// </summary>
    public string Button { get; set; }

    /// <inheritdoc />
    public override string ToString()
    {
        return $"IdentityScopesConsent: {IdentityScopesConsent}, ApiScopesConsent: {ApiScopesConsent}, RememberConsent: {RememberConsent}, ReturnUrl: {ReturnUrl}, Description: {Description}, Button: {Button}";
    }
}

/// <summary>
/// Input state for a single scope row on the consent screen.
/// </summary>
public class ScopeConsentInputModel
{
    /// <summary>
    /// Gets or sets the scope name.
    /// </summary>
    public string Scope { get; set; }

    /// <summary>
    /// Gets or sets the scope description.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets the display name shown to the user.
    /// </summary>
    public string DisplayName { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the scope should be visually emphasized.
    /// </summary>
    public bool Emphasize { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the scope is required.
    /// </summary>
    public bool Required { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the user selected the scope.
    /// </summary>
    public bool Checked { get; set; }

    /// <inheritdoc />
    public override string ToString()
    {
        return $"(Scope: {Scope}, Description: {Description}, DisplayName: {DisplayName}, Emphasize: {Emphasize}, Required: {Required}, Checked: {Checked})";
    }
}
