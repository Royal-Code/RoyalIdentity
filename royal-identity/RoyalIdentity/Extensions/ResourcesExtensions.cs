using Microsoft.IdentityModel.Tokens;
using RoyalIdentity.Models;

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
}