namespace RoyalIdentity.Razor.Components.Account.Consenting;

public class ConsentInputModel
{
    public ScopeConsentInputModel[] IdentityScopesConsent { get; set; }

    public ScopeConsentInputModel[] ApiScopesConsent { get; set; }

    public bool RememberConsent { get; set; }

    public string ReturnUrl { get; set; }

    public string? Description { get; set; }

    public string Button { get; set; }

    public override string ToString()
    {
        return $"IdentityScopesConsent: {IdentityScopesConsent}, ApiScopesConsent: {ApiScopesConsent}, RememberConsent: {RememberConsent}, ReturnUrl: {ReturnUrl}, Description: {Description}, Button: {Button}";
    }
}

public class ScopeConsentInputModel
{
    public string Scope { get; set; }

    public string Description { get; set; }

    public string DisplayName { get; set; }

    public bool Emphasize { get; set; }

    public bool Required { get; set; }

    public bool Checked { get; set; }

    public override string ToString()
    {
        return $"(Scope: {Scope}, Description: {Description}, DisplayName: {DisplayName}, Emphasize: {Emphasize}, Required: {Required}, Checked: {Checked})";
    }
}