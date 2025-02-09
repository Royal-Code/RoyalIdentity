using RoyalIdentity.Models;

namespace RoyalIdentity.Razor.Components.Account.Consenting;

public class ConsentViewModel
{
    public required string ClientName { get; set; }

    public string? ClientUrl { get; set; }

    public string? ClientLogoUrl { get; set; }

    public bool AllowRememberConsent { get; set; }

    public IEnumerable<IdentityResource> IdentityScopes { get; set; }

    public IEnumerable<ApiResource> ApiScopes { get; set; }

    public ScopeConsentInputModel[] CreateIdentityScopes()
    {
        return IdentityScopes.Select(s => new ScopeConsentInputModel()
        {
            Scope = s.Name,
            Description = s.Description,
            DisplayName = s.DisplayName,
            Emphasize = s.Emphasize,
            Required = s.Required,
            Checked = s.Required
        }).ToArray();
    }

    public ScopeConsentInputModel[] CreateApiScopes()
    {
        return ApiScopes.Select(s => new ScopeConsentInputModel()
        {
            Scope = s.Name,
            Description = s.Description,
            DisplayName = s.DisplayName,
            Emphasize = false,
            Required = false,
            Checked = false
        }).ToArray();
    }
}