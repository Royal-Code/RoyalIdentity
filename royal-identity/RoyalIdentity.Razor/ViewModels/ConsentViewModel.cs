using RoyalIdentity.Models.Scopes;

namespace RoyalIdentity.Razor.ViewModels;

public class ConsentViewModel
{
    public required string ClientName { get; set; }

    public string? ClientUrl { get; set; }

    public string? ClientLogoUrl { get; set; }

    public bool AllowRememberConsent { get; set; }

    /// <summary>
    /// True when offline_access (a long-lived refresh token) was requested. Shown as an informational
    /// item on the consent screen; the grant is driven by the scope at validation time, not by a
    /// consent checkbox (Fase 7).
    /// </summary>
    public bool OfflineAccess { get; set; }

    /// <summary>
    /// The requested identity scopes (personal information), not tied to a resource server.
    /// </summary>
    public IReadOnlyCollection<IdentityScope> IdentityScopes { get; set; } = [];

    /// <summary>
    /// The requested application access, grouped by resource server (ADR-012 / Fase 7).
    /// </summary>
    public IReadOnlyCollection<ResourceServerConsentModel> ResourceServers { get; set; } = [];

    /// <summary>
    /// All requested scopes across every resource server, flattened in group order. The flat consent
    /// input list (<see cref="ConsentInputModel.ScopesConsent"/>) is built and indexed in this order,
    /// so the per-group offsets used on the screen line up with the posted indices.
    /// </summary>
    public IEnumerable<Scope> Scopes => ResourceServers.SelectMany(rs => rs.Scopes);

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

/// <summary>
/// A consent group: a resource server with the scopes and protected resources requested from it.
/// Protected resources are audience-only (display) — they have no checkbox; consenting to the
/// scopes authorizes them (ADR-012 scope/resource coherence). See <see cref="ConsentViewModel"/>.
/// </summary>
public class ResourceServerConsentModel
{
    public required string Name { get; set; }

    public required string DisplayName { get; set; }

    public string? Description { get; set; }

    /// <summary>The scopes of this resource server that were requested.</summary>
    public IReadOnlyCollection<Scope> Scopes { get; set; } = [];

    /// <summary>The protected resources (RFC 8707 resource indicators) of this resource server that were requested.</summary>
    public IReadOnlyCollection<ProtectedResource> ProtectedResources { get; set; } = [];
}
