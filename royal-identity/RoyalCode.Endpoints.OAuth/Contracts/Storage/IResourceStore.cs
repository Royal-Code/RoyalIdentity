﻿using RoyalIdentity.Models;

namespace RoyalIdentity.Contracts.Storage;

/// <summary>
/// Resource retrieval
/// </summary>
public interface IResourceStore
{
    /// <summary>
    /// Gets identity resources by scope name.
    /// </summary>
    Task<IEnumerable<IdentityResource>> FindIdentityResourcesByScopeNameAsync(IEnumerable<string> scopeNames);

    /// <summary>
    /// Gets API scopes by scope name.
    /// </summary>
    Task<IEnumerable<ApiScope>> FindApiScopesByNameAsync(IEnumerable<string> scopeNames);

    /// <summary>
    /// Gets API resources by scope name.
    /// </summary>
    Task<IEnumerable<ApiResource>> FindApiResourcesByScopeNameAsync(IEnumerable<string> scopeNames);

    /// <summary>
    /// Gets API resources by API resource name.
    /// </summary>
    Task<IEnumerable<ApiResource>> FindApiResourcesByNameAsync(IEnumerable<string> apiResourceNames);

    /// <summary>
    /// Gets all resources.
    /// </summary>
    Task<Resources> GetAllResourcesAsync();

    /// <summary>
    /// Gets all resources (identity, API scopes, API resources) by scope name.
    /// </summary>
    Task<Resources> FindResourcesByScopeAsync(IEnumerable<string> scopeNames, bool onlyEnabled = false, CancellationToken ct = default);
}