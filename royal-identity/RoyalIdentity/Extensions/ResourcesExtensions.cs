using RoyalIdentity.Models;
using RoyalIdentity.Models.Scopes;

namespace RoyalIdentity.Extensions;

public static class ResourcesExtensions
{
    /// <summary>
    /// Resolves the signing-algorithm filter for the access token (ADR-010, decisao #a).
    /// The Realm always provides the order and availability; resource servers and the client act only
    /// as a restrictive filter, hierarchically: requested resource servers restrict first (multi-RS:
    /// intersection), else the client restricts, else no extra restriction (Realm only). Resource server
    /// and client filters are never combined, so different restrictions never yield an empty set.
    /// </summary>
    internal static HashSet<string> ResolveAccessTokenSigningAlgorithms(this RequestedResources resources, Client client)
    {
        var fromResourceServers = resources.ResourceServers.FindMatchingSigningAlgorithms();
        if (fromResourceServers.Count > 0)
            return fromResourceServers;

        if (client.AllowedAccessTokenSigningAlgorithms.Count > 0)
            return [.. client.AllowedAccessTokenSigningAlgorithms];

        return [];
    }

    internal static HashSet<string> FindMatchingSigningAlgorithms(this IEnumerable<ResourceServer> apiResources)
    {
        var apis = apiResources.ToList();

        if (apis.IsNullOrEmpty())
            return [];

        // only one API resource request, forward the allowed signing algorithms (if any)
        if (apis.Count == 1)
        {
            return apis[0].AllowedAccessTokenSigningAlgorithms;
        }

        var allAlgorithms = apis.Where(r => r.AllowedAccessTokenSigningAlgorithms.Any())
            .Select(r => r.AllowedAccessTokenSigningAlgorithms).ToList();

        // resources need to agree on allowed signing algorithms
        if (allAlgorithms.Count is 0)
            return [];

        var allowedAlgorithms = allAlgorithms.IntersectMany().ToHashSet();
        if (allowedAlgorithms.Count is 0)
            throw new InvalidOperationException(
                "Signing algorithms requirements for requested resources are not compatible.");

        return allowedAlgorithms.ToHashSet();
    }

    /// <summary>
    /// Filters out protocol claims like amr, nonce etc..
    /// </summary>
    /// <param name="claimTypes">The claim types.</param>
    public static IReadOnlyList<string> FilterRequestedClaimTypes(this IReadOnlyList<string> claimTypes)
    {
        var claimTypesToFilter = claimTypes.Where(x => Filters.ClaimsServiceFilterClaimTypes.Contains(x));
        return [.. claimTypes.Except(claimTypesToFilter)];
    }

    /// <summary>
    /// Gets the requested Identity claim types.
    /// </summary>
    /// <param name="resources">Requested resources.</param>
    /// <returns>A list of requested Identity claim types.</returns>
    public static IReadOnlyList<string> RequestedIdentityClaimTypes(this RequestedResources resources)
    {
        var types = resources.IdentityScopes.SelectMany(r => r.UserClaims).ToList();

        // filter so we don't ask for claim types that we will eventually filter out
        return FilterRequestedClaimTypes(types);
    }
}