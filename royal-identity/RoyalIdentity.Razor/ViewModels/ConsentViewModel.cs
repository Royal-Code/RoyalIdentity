using RoyalIdentity.Models.Scopes;

namespace RoyalIdentity.Razor.ViewModels;

public class ConsentViewModel
{
    public required string ClientName { get; set; }

    public string? ClientUrl { get; set; }

    public string? ClientLogoUrl { get; set; }

    public bool AllowRememberConsent { get; set; }

    public IEnumerable<IdentityScope> IdentityScopes { get; set; }

    public IEnumerable<Scope> Scopes { get; set; }

    public ICollection<ScopeConsentInputModel> CreateIdentityScopes()
    {
        return IdentityScopes.Select(s => new ScopeConsentInputModel
        {
            Scope = s.Name,
            Description = s.Description,
            DisplayName = s.DisplayName,
            Emphasize = s.Emphasize,
            Required = s.Required,
            Checked = true
        }).ToArray();
    }

    public ICollection<ScopeConsentInputModel> CreateScopes()
    {
        return Scopes.Select(s => new ScopeConsentInputModel
        {
            Scope = s.Name,
            Description = s.Description,
            DisplayName = s.DisplayName,
            Emphasize = s.Emphasize,
            Required = s.Required,
            Checked = true
        }).ToArray();
    }
}
