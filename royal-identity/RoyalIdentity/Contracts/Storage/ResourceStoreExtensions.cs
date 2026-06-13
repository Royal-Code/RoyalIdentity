using RoyalIdentity.Models.Scopes;
using static RoyalIdentity.Options.Constants;

namespace RoyalIdentity.Contracts.Storage;

/// <summary>
/// The outcome of resolving the resources effectively authorized for a token-endpoint grant
/// (authorization_code / refresh_token). On failure carries the OAuth error to return; on success
/// carries the resolved <see cref="RequestedResources"/>.
/// </summary>
public readonly record struct ResourceResolution(
    RequestedResources? Resources,
    string? Error,
    string? ErrorDescription,
    string? Detail)
{
    public bool IsSuccess => Error is null;

    public static ResourceResolution Ok(RequestedResources resources) => new(resources, null, null, null);

    public static ResourceResolution Fail(string error, string description, string? detail = null)
        => new(null, error, description, detail);
}

public static class ResourceStoreExtensions
{
    /// <summary>
    /// Resolves the effective resources for a token-endpoint grant from a previously authorized set,
    /// applying RFC 8707 subset narrowing and the ADR-012 validation (invalid_target / invalid_scope /
    /// scope-resource coherence). When <paramref name="requestedResourceUris"/> is empty, the full
    /// authorized set is used; otherwise it must be a subset of the authorized set.
    /// </summary>
    public static async Task<ResourceResolution> ResolveAuthorizedSubsetAsync(
        this IResourceStore store,
        IEnumerable<string> scopeNames,
        IEnumerable<string> authorizedResourceUris,
        IReadOnlyCollection<string> requestedResourceUris,
        bool onlyEnabled,
        CancellationToken ct)
    {
        var authorizedScopeNames = scopeNames.ToArray();
        var authorized = authorizedResourceUris.ToHashSet(StringComparer.Ordinal);

        IEnumerable<string> effectiveResourceUris = authorized;
        IEnumerable<string> effectiveScopeNames = authorizedScopeNames;
        if (requestedResourceUris.Count is not 0)
        {
            var unauthorized = requestedResourceUris.Where(uri => !authorized.Contains(uri)).ToArray();
            if (unauthorized.Length is not 0)
            {
                return ResourceResolution.Fail(
                    Oidc.Token.Errors.InvalidTarget,
                    "resource indicators requested were not authorized",
                    string.Join(" ", unauthorized));
            }

            effectiveResourceUris = requestedResourceUris;

            var requestedResources = await store.FindRequestedResourcesAsync([], effectiveResourceUris, onlyEnabled, ct);
            if (requestedResources.HasInvalidTargets)
            {
                return ResourceResolution.Fail(
                    Oidc.Token.Errors.InvalidTarget,
                    "resource indicators requested are invalid",
                    requestedResources.GetInvalidTargets());
            }

            var resolvedAuthorizedScopes = await store.FindRequestedResourcesAsync(authorizedScopeNames, [], onlyEnabled, ct);
            if (!resolvedAuthorizedScopes.IsValid)
            {
                return ResourceResolution.Fail(
                    Oidc.Token.Errors.InvalidScope,
                    "scopes requested are invalid or inactive",
                    resolvedAuthorizedScopes.GetInvalidScopes());
            }

            effectiveScopeNames = SelectScopeNamesForResourceSubset(
                authorizedScopeNames,
                resolvedAuthorizedScopes,
                requestedResources.ResourceServers);
        }

        var resources = await store.FindRequestedResourcesAsync(effectiveScopeNames, effectiveResourceUris, onlyEnabled, ct);

        if (resources.HasInvalidTargets)
        {
            return ResourceResolution.Fail(
                Oidc.Token.Errors.InvalidTarget,
                "resource indicators requested are invalid",
                resources.GetInvalidTargets());
        }

        if (!resources.IsValid)
        {
            return ResourceResolution.Fail(
                Oidc.Token.Errors.InvalidScope,
                "scopes requested are invalid or inactive",
                resources.GetInvalidScopes());
        }

        if (!resources.IsScopeResourceCoherent())
        {
            return ResourceResolution.Fail(
                Oidc.Token.Errors.InvalidTarget,
                "scope requires a matching resource indicator");
        }

        return ResourceResolution.Ok(resources);
    }

    private static string[] SelectScopeNamesForResourceSubset(
        IEnumerable<string> scopeNames,
        RequestedResources resolvedAuthorizedScopes,
        ICollection<ResourceServer> selectedResourceServers)
    {
        var selectedResourceServerNames = selectedResourceServers
            .Select(rs => rs.Name)
            .ToHashSet(StringComparer.Ordinal);

        var scopeOwners = resolvedAuthorizedScopes.ResourceServers
            .SelectMany(rs => rs.Scopes.Select(scope => (ScopeName: scope.Name, Server: rs)))
            .ToDictionary(x => x.ScopeName, x => x.Server, StringComparer.Ordinal);

        var includedScopeNames = resolvedAuthorizedScopes.IdentityScopes
            .Select(scope => scope.Name)
            .ToHashSet(StringComparer.Ordinal);

        if (resolvedAuthorizedScopes.OfflineAccess)
            includedScopeNames.Add(Server.StandardScopes.OfflineAccess);

        foreach (var scope in resolvedAuthorizedScopes.Scopes)
        {
            var include = !scopeOwners.TryGetValue(scope.Name, out var owner)
                || owner.ProtectedResources.Count is 0
                || selectedResourceServerNames.Contains(owner.Name);

            if (include)
                includedScopeNames.Add(scope.Name);
        }

        return scopeNames
            .Where(includedScopeNames.Contains)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }
}
