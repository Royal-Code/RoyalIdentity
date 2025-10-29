using RoyalIdentity.Models.Resources;
using RoyalIdentity.Models.Scopes;

namespace RoyalIdentity.Contracts.Storage;

/// <summary>
/// Resource retrieval
/// </summary>
public interface IResourceStore
{
    /// <summary>
    /// Gets identity resources by scope name.
    /// </summary>
    [Obsolete("Use only FindResourcesByScopeAsync")]
    Task<IEnumerable<IdentityScope>> FindIdentityResourcesByScopeNameAsync(IEnumerable<string> scopeNames, CancellationToken ct = default);

    /// <summary>
    /// Gets API scopes by scope name.
    /// </summary>
    [Obsolete("Use only FindResourcesByScopeAsync")] 
    Task<IEnumerable<ApiScope>> FindApiScopesByScopeNameAsync(IEnumerable<string> scopeNames, CancellationToken ct = default);

    /// <summary>
    /// Gets API resources by scope name.
    /// </summary>
    [Obsolete("Use only FindResourcesByScopeAsync")] 
    Task<IEnumerable<ApiResource>> FindApiResourcesByScopeNameAsync(IEnumerable<string> scopeNames, CancellationToken ct = default);

    /// <summary>
    /// Gets all resources.
    /// </summary>
    Task<AllScopes> GetAllResourcesAsync(CancellationToken ct = default);

    /// <summary>
    /// Gets all enabled resources.
    /// </summary>
    /// <param name="ct"></param>
    /// <returns></returns>
    Task<AllScopes> GetAllEnabledResourcesAsync(CancellationToken ct = default);

    /// <summary>
    /// Gets all requested resources (identity, API scopes, API resources) by scope name.
    /// </summary>
    Task<RequestedScopes> FindResourcesByScopeAsync(IEnumerable<string> scopeNames, bool onlyEnabled = false, CancellationToken ct = default);
}
