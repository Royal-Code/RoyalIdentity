namespace RoyalIdentity.Models.Scopes;

/// <summary>
/// A snapshot of all resource servers and identity scopes of a realm.
/// </summary>
public class AllScopes
{
    public AllScopes(
        IReadOnlyList<ResourceServer> resourceServers,
        IReadOnlyList<IdentityScope> identityScopes)
    {
        ResourceServers = resourceServers;
        IdentityScopes = identityScopes;
    }

    public IReadOnlyList<ResourceServer> ResourceServers { get; }

    public IReadOnlyList<IdentityScope> IdentityScopes { get; }

    /// <summary>
    /// All scopes across all resource servers.
    /// </summary>
    public IEnumerable<Scope> Scopes => ResourceServers.SelectMany(rs => rs.Scopes);
}
