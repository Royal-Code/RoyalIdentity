using RoyalIdentity.Extensions;
using RoyalIdentity.Options;
using System.Security.Claims;

namespace RoyalIdentity.Models.Scopes;

/// <summary>
/// Models the identity scopes, resource servers and scopes resolved from a client request.
/// </summary>
public class RequestedResources
{
    public RequestedResources() { }

    public RequestedResources(RequestedResources other)
        : this(other.IdentityScopes, other.ResourceServers, other.Scopes)
    {
        OfflineAccess = other.OfflineAccess;
    }

    public RequestedResources(
        IEnumerable<IdentityScope> identityScopes,
        IEnumerable<ResourceServer> resourceServers,
        IEnumerable<Scope> scopes)
    {
        IdentityScopes = new HashSet<IdentityScope>(identityScopes);
        ResourceServers = new HashSet<ResourceServer>(resourceServers);
        Scopes = new HashSet<Scope>(scopes);
    }

    /// <summary>
    /// True when the openid identity scope was requested.
    /// </summary>
    public bool IsOpenId => IdentityScopes.Any(x => x.Name == Server.StandardScopes.OpenId);

    /// <summary>
    /// True when offline_access (refresh token) was requested.
    /// </summary>
    public bool OfflineAccess { get; set; }

    /// <summary>
    /// The raw requested scope names.
    /// </summary>
    public HashSet<string> RequestedScopeNames { get; } = [];

    /// <summary>
    /// The resource servers owning the requested scopes (and any directly requested resource server).
    /// Used to derive the audience.
    /// </summary>
    public ICollection<ResourceServer> ResourceServers { get; } = [];

    /// <summary>
    /// The requested scopes.
    /// </summary>
    public ICollection<Scope> Scopes { get; } = [];

    /// <summary>
    /// The requested identity scopes.
    /// </summary>
    public ICollection<IdentityScope> IdentityScopes { get; } = [];

    /// <summary>
    /// Scopes requested but invalid (not found or disabled). A single bucket of invalid scopes
    /// (apontamento 3.1): disabled scopes are treated uniformly as invalid.
    /// </summary>
    public HashSet<string> MissingScopes { get; } = [];

    /// <summary>
    /// The raw requested resource indicator URIs (RFC 8707 <c>resource</c> parameter).
    /// </summary>
    public HashSet<string> RequestedResourceUris { get; } = [];

    /// <summary>
    /// The requested protected resources (resolved from <see cref="RequestedResourceUris"/>).
    /// </summary>
    public ICollection<ProtectedResource> ProtectedResources { get; } = [];

    /// <summary>
    /// Requested resource indicators that are invalid (malformed, unknown, or of a disabled resource server).
    /// Maps to the <c>invalid_target</c> error (RFC 8707).
    /// </summary>
    public HashSet<string> InvalidTargets { get; } = [];

    /// <summary>
    /// The scope claims to emit in the token: only the requested scopes, identity scopes and offline_access.
    /// </summary>
    public IEnumerable<Claim> ToScopeClaims()
    {
        if (OfflineAccess)
            yield return new Claim(Jwt.ClaimTypes.Scope, Server.StandardScopes.OfflineAccess);

        foreach (var scope in Scopes.Select(s => s.Name).Distinct())
        {
            yield return new Claim(Jwt.ClaimTypes.Scope, scope);
        }

        foreach (var identity in IdentityScopes.Select(s => s.Name).Distinct())
        {
            yield return new Claim(Jwt.ClaimTypes.Scope, identity);
        }
    }

    /// <summary>
    /// The user claim types requested through the identity scopes.
    /// </summary>
    public IReadOnlyList<string> GetRequestedIdentityClaimsTypes()
    {
        return IdentityScopes.SelectMany(r => r.UserClaims).ToList();
    }

    /// <summary>
    /// The distinct audiences for the request. A requested resource indicator (RFC 8707) emits its
    /// <see cref="ProtectedResource.ResourceUri"/> and overrides the legacy <c>Audience</c> of its owning
    /// resource server; resource servers without a requested resource use <see cref="ResourceServer.GetAudience"/>.
    /// </summary>
    public IEnumerable<string> GetAudiences()
    {
        var audiences = new HashSet<string>(StringComparer.Ordinal);

        // resource path (RFC 8707): explicit audiences
        foreach (var resource in ProtectedResources)
            audiences.Add(resource.ResourceUri);

        var requestedResourceUris = ProtectedResources.Select(r => r.ResourceUri).ToHashSet(StringComparer.Ordinal);

        // scope path: the resource server's legacy audience, unless overridden by a requested resource of that server
        foreach (var rs in ResourceServers)
        {
            var overriddenByResource = rs.ProtectedResources.Any(pr => requestedResourceUris.Contains(pr.ResourceUri));
            if (!overriddenByResource)
                audiences.Add(rs.GetAudience());
        }

        return audiences;
    }

    /// <summary>
    /// True when the requested identity scopes and scopes are all contained in the consented scopes.
    /// </summary>
    public bool IntersectConsentScopes(IEnumerable<string> scopes)
    {
        var requestedScopes = IdentityScopes.Select(s => s.Name).Concat(Scopes.Select(s => s.Name));
        var intersect = requestedScopes.Intersect(scopes);
        return intersect.Count() == requestedScopes.Count();
    }

    public void CopyTo(RequestedResources other)
    {
        other.OfflineAccess = OfflineAccess;

        other.ResourceServers.Clear();
        other.ResourceServers.AddRange(ResourceServers);

        other.IdentityScopes.Clear();
        other.IdentityScopes.AddRange(IdentityScopes);

        other.Scopes.Clear();
        other.Scopes.AddRange(Scopes);

        other.RequestedScopeNames.Clear();
        other.RequestedScopeNames.AddRange(RequestedScopeNames);

        other.MissingScopes.Clear();
        other.MissingScopes.AddRange(MissingScopes);

        other.RequestedResourceUris.Clear();
        other.RequestedResourceUris.AddRange(RequestedResourceUris);

        other.ProtectedResources.Clear();
        other.ProtectedResources.AddRange(ProtectedResources);

        other.InvalidTargets.Clear();
        other.InvalidTargets.AddRange(InvalidTargets);
    }

    /// <summary>
    /// Count of resolved resources, excluding resources not found or disabled.
    /// </summary>
    public int Count => (OfflineAccess ? 1 : 0) + ResourceServers.Count + IdentityScopes.Count + Scopes.Count;

    public bool IsValid => MissingScopes.Count is 0;

    public bool Any() => OfflineAccess ||
        ResourceServers.Count is not 0 ||
        IdentityScopes.Count is not 0 ||
        Scopes.Count is not 0 ||
        ProtectedResources.Count is not 0;

    public bool None() => !Any();

    public string GetInvalidScopes() => string.Join(" ", MissingScopes);

    /// <summary>True when one or more requested resource indicators are invalid (RFC 8707 <c>invalid_target</c>).</summary>
    public bool HasInvalidTargets => InvalidTargets.Count is not 0;

    public string GetInvalidTargets() => string.Join(" ", InvalidTargets);
}
