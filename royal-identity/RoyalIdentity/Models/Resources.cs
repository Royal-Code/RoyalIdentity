using System.Diagnostics.CodeAnalysis;
using RoyalIdentity.Extensions;
using RoyalIdentity.Options;

namespace RoyalIdentity.Models;

/// <summary>
/// Models a collection of identity and API resources.
/// </summary>
public class Resources
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Resources"/> class.
    /// </summary>
    public Resources() { }

    /// <summary>
    /// Initializes a new instance of the <see cref="Resources"/> class.
    /// </summary>
    /// <param name="other">The other.</param>
    public Resources(Resources other)
        : this(other.IdentityResources, other.ApiResources, other.ApiScopes)
    {
        OfflineAccess = other.OfflineAccess;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Resources"/> class.
    /// </summary>
    /// <param name="identityResources">The identity resources.</param>
    /// <param name="apiResources">The API resources.</param>
    /// <param name="apiScopes">The API scopes.</param>
    public Resources(
        IEnumerable<IdentityResource> identityResources,
        IEnumerable<ApiResource> apiResources,
        IEnumerable<ApiScope> apiScopes)
    {
        IdentityResources = new HashSet<IdentityResource>(identityResources);
        ApiResources = new HashSet<ApiResource>(apiResources);
        ApiScopes = new HashSet<ApiScope>(apiScopes);
    }

    /// <summary>
    /// Gets a value indicating whether this instance has identity resources.
    /// </summary>
    public bool IsOpenId => IdentityResources.Any(x => x.Name == ServerConstants.StandardScopes.OpenId);

    /// <summary>
    /// Gets or sets a value indicating whether [offline access].
    /// </summary>
    /// <value>
    ///   <c>true</c> if [offline access]; otherwise, <c>false</c>.
    /// </value>
    public bool OfflineAccess { get; set; }

    /// <summary>
    /// Gets or sets the requested scopes.
    /// </summary>
    public HashSet<string> RequestedScopes { get; } = [];

    /// <summary>
    /// Gets or sets the identity resources.
    /// </summary>
    public ICollection<IdentityResource> IdentityResources { get; } = [];

    /// <summary>
    /// Gets or sets the API scopes.
    /// </summary>
    public ICollection<ApiScope> ApiScopes { get; } = [];

    /// <summary>
    /// Gets or sets the API resources.
    /// </summary>
    public ICollection<ApiResource> ApiResources { get; } = [];

    /// <summary>
    /// Scopes requested but not found or inactive.
    /// </summary>
    public HashSet<string> MissingScopes { get; } = [];

    /// <summary>
    /// Copies the resources to the specified other.
    /// </summary>
    /// <param name="other"></param>
    public void CopyTo(Resources other)
    {
        other.OfflineAccess = OfflineAccess;
        
        other.IdentityResources.Clear();
        other.IdentityResources.AddRange(IdentityResources);
        
        other.ApiResources.Clear();
        other.ApiResources.AddRange(ApiResources);
        
        other.ApiScopes.Clear();
        other.ApiScopes.AddRange(ApiScopes);

        other.MissingScopes.Clear();
        other.MissingScopes.AddRange(MissingScopes);
    }

    public bool TryFindIdentityResourceByName(string name, [NotNullWhen(true)] out IdentityResource? identityResource)
    {
        identityResource = IdentityResources.FirstOrDefault(x => x.Name == name);
        return identityResource != null;
    }

    public bool TryFindApiScopeByName(string name, [NotNullWhen(true)] out ApiScope? apiScope)
    {
        apiScope = ApiScopes.FirstOrDefault(x => x.Name == name);
        return apiScope != null;
    }

    public IEnumerable<ApiResource> FindApiResourceByScopeName(string name)
    {
        return ApiResources.Where(r => r.Scopes.Any(s => s.Contains(name)));
    }

    /// <summary>
    /// Count all load resources, except for resources not found or inactive.
    /// </summary>
    public int Count => (OfflineAccess ? 1 : 0) + IdentityResources.Count + ApiResources.Count + ApiScopes.Count;

    public bool IsValid => MissingScopes.Count is 0 && Count == RequestedScopes.Count;

    public bool Any() => OfflineAccess ||
        IdentityResources.Count is not 0 ||
        ApiResources.Count is not 0 ||
        ApiScopes.Count is not 0;

    public bool None() => !Any();
}
