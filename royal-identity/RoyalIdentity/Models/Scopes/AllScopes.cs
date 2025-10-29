namespace RoyalIdentity.Models.Scopes;

/// <summary>
/// A class with all scopes.
/// </summary>
public class AllScopes
{
    public AllScopes(
        IReadOnlyList<ResourceServer> resourceServers,
        IReadOnlyList<ApiResource> resources,
        IReadOnlyList<ApiScope> apiScopes,
        IReadOnlyList<IdentityScope> identityScopes)
    {
        ResourceServers = resourceServers;
        Resources = resources;
        ApiScopes = apiScopes;
        IdentityScopes = identityScopes;
    }

    public IReadOnlyList<ResourceServer> ResourceServers { get; }

    public IReadOnlyList<ApiResource> Resources { get; }

    public IReadOnlyList<ApiScope> ApiScopes { get; }

    public IReadOnlyList<IdentityScope> IdentityScopes { get; }
}
