using RoyalIdentity.Models;
using RoyalIdentity.Models.Scopes;

namespace RoyalIdentity.Extensions;

/// <summary>
/// The outcome of resolving the access-token signing-algorithm filter. On success carries the resolved
/// set (empty = no extra restriction, Realm only); on failure carries the incompatibility error so the
/// caller can signal it (e.g. <c>invalid_request</c>) instead of throwing.
/// </summary>
public readonly record struct SigningAlgorithmsResolution(HashSet<string> Algorithms, string? Error)
{
    public bool IsCompatible => Error is null;

    public static SigningAlgorithmsResolution Ok(HashSet<string> algorithms) => new(algorithms, null);

    public static SigningAlgorithmsResolution Incompatible(string error) => new([], error);
}

public static class ResourcesExtensions
{
    /// <summary>
    /// Resolves the signing-algorithm filter for the access token (ADR-010, decisao #a).
    /// The Realm always provides the order and availability; resource servers and the client act only
    /// as a restrictive filter, hierarchically: requested resource servers restrict first (multi-RS:
    /// intersection), else the client restricts, else no extra restriction (Realm only). Resource server
    /// and client filters are never combined, so different restrictions never yield an empty set.
    /// Returns a <see cref="SigningAlgorithmsResolution"/> carrying the resolved set, or an error when
    /// multiple requested resource servers impose mutually incompatible signing algorithms.
    /// </summary>
    internal static SigningAlgorithmsResolution ResolveAccessTokenSigningAlgorithms(this RequestedResources resources, Client client)
    {
        var (fromResourceServers, error) = resources.ResourceServers.FindMatchingSigningAlgorithms();
        if (error is not null)
            return SigningAlgorithmsResolution.Incompatible(error);

        if (fromResourceServers.Count > 0)
            return SigningAlgorithmsResolution.Ok(fromResourceServers);

        if (client.AllowedAccessTokenSigningAlgorithms.Count > 0)
            return SigningAlgorithmsResolution.Ok([.. client.AllowedAccessTokenSigningAlgorithms]);

        return SigningAlgorithmsResolution.Ok([]);
    }

    internal static (HashSet<string> Algorithms, string? Error) FindMatchingSigningAlgorithms(this IEnumerable<ResourceServer> apiResources)
    {
        var apis = apiResources.ToList();

        if (apis.IsNullOrEmpty())
            return ([], null);

        // only one API resource request, forward the allowed signing algorithms (if any)
        if (apis.Count == 1)
        {
            return (apis[0].AllowedAccessTokenSigningAlgorithms, null);
        }

        var allAlgorithms = apis.Where(r => r.AllowedAccessTokenSigningAlgorithms.Count > 0)
            .Select(r => r.AllowedAccessTokenSigningAlgorithms).ToList();

        // resources need to agree on allowed signing algorithms
        if (allAlgorithms.Count is 0)
            return ([], null);

        var allowedAlgorithms = allAlgorithms.IntersectMany().ToHashSet();
        if (allowedAlgorithms.Count is 0)
            return ([], "Signing algorithms requirements for requested resources are not compatible.");

        return (allowedAlgorithms, null);
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