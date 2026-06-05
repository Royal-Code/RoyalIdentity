using RoyalIdentity.Extensions;
using RoyalIdentity.Models.Scopes;
using RoyalIdentity.Options;
using System.Collections.Generic;
using System.Security.Claims;

namespace RoyalIdentity.Models.Resources;

/// <summary>
/// Models a collection of identity and API resources.
/// </summary>
public class RequestedScopes
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Resources.RequestedScopes"/> class.
    /// </summary>
    public RequestedScopes() { }

    /// <summary>
    /// Initializes a new instance of the <see cref="Resources.RequestedScopes"/> class.
    /// </summary>
    /// <param name="other">The other.</param>
    public RequestedScopes(RequestedScopes other)
        : this(other.IdentityResources, other.ApiResources, other.ApiScopes)
    {
        OfflineAccess = other.OfflineAccess;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Resources.RequestedScopes"/> class.
    /// </summary>
    /// <param name="identityResources">The identity resources.</param>
    /// <param name="apiResources">The API resources.</param>
    /// <param name="apiScopes">The API scopes.</param>
    public RequestedScopes(
        IEnumerable<IdentityScope> identityResources,
        IEnumerable<ApiResource> apiResources,
        IEnumerable<ApiScope> apiScopes)
    {
        IdentityResources = new HashSet<IdentityScope>(identityResources);
        ApiResources = new HashSet<ApiResource>(apiResources);
        ApiScopes = new HashSet<ApiScope>(apiScopes);
    }

    /// <summary>
    /// Gets a value indicating whether this instance has identity resources.
    /// </summary>
    public bool IsOpenId => IdentityResources.Any(x => x.Name == Server.StandardScopes.OpenId);

    /// <summary>
    /// Gets or sets a value indicating whether [offline access].
    /// </summary>
    /// <value>
    ///   <c>true</c> if [offline access]; otherwise, <c>false</c>.
    /// </value>
    public bool OfflineAccess { get; set; }

    /// <summary>
    /// Get the requested scopes.
    /// </summary>
    public HashSet<string> Scopes { get; } = [];

    /// <summary>
    /// Get the resource servers.
    /// </summary>
    public ICollection<ResourceServer> ResourceServers { get; } = [];

    /// <summary>
    /// Get the API scopes.
    /// </summary>
    public ICollection<ApiScope> ApiScopes { get; } = [];

    /// <summary>
    /// Get the API resources.
    /// </summary>
    public ICollection<ApiResource> ApiResources { get; } = [];

    /// <summary>
    /// Get the identity resources.
    /// </summary>
    public ICollection<IdentityScope> IdentityResources { get; } = [];

    /// <summary>
    /// Scopes requested but not found or inactive.
    /// </summary>
    public HashSet<string> MissingScopes { get; } = [];

    /// <summary>
    /// Scopes that are disabled.
    /// </summary>
    public HashSet<string> DisabledScopes { get; } = [];

    public IEnumerable<Claim> ToScopeClaims()
    {
        if (OfflineAccess)
            yield return new Claim(Jwt.ClaimTypes.Scope, Server.StandardScopes.OfflineAccess);

        // api scopes.

        foreach (var scope in ResourceServers.SelectMany(rs => rs.Resources).SelectMany(r => r.Scopes).Select(s => s.Name).Distinct())
        {
            yield return new Claim(Jwt.ClaimTypes.Scope, scope);
        }

        foreach (var resource in ApiResources.SelectMany(ar => ar.Scopes).Select(s => s.Name).Distinct())
        {
            yield return new Claim(Jwt.ClaimTypes.Scope, resource);
        }

        foreach (var resource in ApiScopes.Select(s => s.Name).Distinct())
        {
            yield return new Claim(Jwt.ClaimTypes.Scope, resource);
        }

        // identity scopes.

        foreach (var resource in IdentityResources.Select(s => s.Name).Distinct())
        {
            yield return new Claim(Jwt.ClaimTypes.Scope, resource);
        }

        foreach (var scope in ResourceServers.SelectMany(rs => rs.IdentityScopes).Select(s => s.Name).Distinct())
        {
            yield return new Claim(Jwt.ClaimTypes.Scope, scope);
        }
    }

    public IReadOnlyList<string> GetRequestedIdentityClaimsTypes()
    {
        var types = IdentityResources.SelectMany(r => r.UserClaims).ToList();
        types.AddRange(ResourceServers.SelectMany(rs => rs.IdentityScopes).SelectMany(s => s.UserClaims));
        return types;
    }

    /// <summary>
    /// Intersects the consent scopes.
    /// </summary>
    /// <param name="scopes">The consent scopes.</param>
    /// <returns>
    ///     True if the consent scopes intersect with the requested scopes, otherwise false.
    /// </returns>
    public bool IntersectConsentScopes(IEnumerable<string> scopes)
    {
        var requestedScopes = IdentityResources.Select(s => s.Name).Concat(ApiScopes.Select(s => s.Name));
        var intersect = requestedScopes.Intersect(scopes);
        return intersect.Count() == requestedScopes.Count();
    }

    /// <summary>
    /// Copies the resources to the specified other.
    /// </summary>
    /// <param name="other"></param>
    public void CopyTo(RequestedScopes other)
    {
        other.OfflineAccess = OfflineAccess;
        
        other.ResourceServers.Clear();
        other.ResourceServers.AddRange(ResourceServers);

        other.IdentityResources.Clear();
        other.IdentityResources.AddRange(IdentityResources);
        
        other.ApiResources.Clear();
        other.ApiResources.AddRange(ApiResources);
        
        other.ApiScopes.Clear();
        other.ApiScopes.AddRange(ApiScopes);

        other.MissingScopes.Clear();
        other.MissingScopes.AddRange(MissingScopes);

        other.DisabledScopes.Clear();
        other.DisabledScopes.AddRange(DisabledScopes);
    }

    /// <summary>
    /// Count all load resources, except for resources not found or inactive.
    /// </summary>
    public int Count => (OfflineAccess ? 1 : 0) + ResourceServers.Count + IdentityResources.Count + ApiResources.Count + ApiScopes.Count;

    public bool IsValid => MissingScopes.Count is 0 && DisabledScopes.Count is 0 && Count == Scopes.Count;

    public bool Any() => OfflineAccess ||
        ResourceServers.Count is not 0 ||
        IdentityResources.Count is not 0 ||
        ApiResources.Count is not 0 ||
        ApiScopes.Count is not 0;

    public bool None() => !Any();

    public string GetInvalidScopes() => string.Join(" ", MissingScopes.Concat(DisabledScopes));
}
