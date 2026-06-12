using RoyalIdentity.Models.Scopes;

namespace RoyalIdentity.Contracts.Storage;

/// <summary>
/// Resource retrieval
/// </summary>
public interface IResourceStore
{
    /// <summary>
    /// Gets all resources.
    /// </summary>
    Task<AllScopes> GetAllResourcesAsync(CancellationToken ct = default);

    /// <summary>
    /// Gets all enabled resources.
    /// </summary>
    Task<AllScopes> GetAllEnabledResourcesAsync(CancellationToken ct = default);

    /// <summary>
    /// Gets the requested resources (identity scopes, resource servers and scopes) by scope name.
    /// </summary>
    Task<RequestedResources> FindResourcesByScopeAsync(IEnumerable<string> scopeNames, bool onlyEnabled = false, CancellationToken ct = default);

    /// <summary>
    /// Gets the requested resources by scope name and resource indicator URI (RFC 8707 <c>resource</c>).
    /// Unknown, malformed, or disabled resource indicators are reported in <see cref="RequestedResources.InvalidTargets"/>.
    /// </summary>
    Task<RequestedResources> FindRequestedResourcesAsync(IEnumerable<string> scopeNames, IEnumerable<string> resourceUris, bool onlyEnabled = false, CancellationToken ct = default);
}
