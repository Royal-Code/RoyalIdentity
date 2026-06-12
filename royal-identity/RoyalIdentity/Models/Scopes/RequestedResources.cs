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
    /// Scopes requested but not found.
    /// </summary>
    public HashSet<string> MissingScopes { get; } = [];

    /// <summary>
    /// Scopes requested but disabled.
    /// </summary>
    public HashSet<string> DisabledScopes { get; } = [];

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
    /// The distinct audiences for the requested scopes.
    /// </summary>
    public IEnumerable<string> GetAudiences()
    {
        return ResourceServers.Select(rs => rs.GetAudience()).Distinct();
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

        other.MissingScopes.Clear();
        other.MissingScopes.AddRange(MissingScopes);

        other.DisabledScopes.Clear();
        other.DisabledScopes.AddRange(DisabledScopes);
    }

    /// <summary>
    /// Count of resolved resources, excluding resources not found or disabled.
    /// </summary>
    public int Count => (OfflineAccess ? 1 : 0) + ResourceServers.Count + IdentityScopes.Count + Scopes.Count;

    public bool IsValid => MissingScopes.Count is 0 && DisabledScopes.Count is 0;

    public bool Any() => OfflineAccess ||
        ResourceServers.Count is not 0 ||
        IdentityScopes.Count is not 0 ||
        Scopes.Count is not 0;

    public bool None() => !Any();

    public string GetInvalidScopes() => string.Join(" ", MissingScopes.Concat(DisabledScopes));
}
