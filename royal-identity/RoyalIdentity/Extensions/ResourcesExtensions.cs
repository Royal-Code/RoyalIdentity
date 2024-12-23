using Microsoft.IdentityModel.Tokens;
using RoyalIdentity.Models;
using RoyalIdentity.Options;

namespace RoyalIdentity.Extensions;

public static class ResourcesExtensions
{
    internal static HashSet<string> FindMatchingSigningAlgorithms(this IEnumerable<ApiResource> apiResources)
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
    public static IEnumerable<string> FilterRequestedClaimTypes(List<string> claimTypes)
    {
        var claimTypesToFilter = claimTypes.Where(x => Constants.Filters.ClaimsServiceFilterClaimTypes.Contains(x));
        return claimTypes.Except(claimTypesToFilter);
    }

    /// <summary>
    /// Gets the requested Identity claim types.
    /// </summary>
    /// <param name="resources">Requested resources.</param>
    /// <returns>A list of requested Identity claim types.</returns>
    public static IReadOnlyList<string> RequestedIdentityClaimTypes(this Resources resources)
    {
        var types = resources.IdentityResources.SelectMany(r => r.UserClaims).ToList();

        // filter so we don't ask for claim types that we will eventually filter out
        return FilterRequestedClaimTypes(types).ToList();
    }

    public static IReadOnlyList<string> RequestedResourcesClaimTypes(this Resources resources)
    {
        // fetch all resource claims that need to go into the access token
        var types = resources.ApiResources.SelectMany(api => api.UserClaims).ToList();
        types.AddRange(resources.ApiScopes.SelectMany(scope => scope.UserClaims));

        // filter so we don't ask for claim types that we will eventually filter out
        return FilterRequestedClaimTypes(types).ToList();
    }
}