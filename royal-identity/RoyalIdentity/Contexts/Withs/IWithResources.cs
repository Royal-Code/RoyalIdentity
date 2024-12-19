using RoyalIdentity.Models;

namespace RoyalIdentity.Contexts.Withs;

public interface IWithResources : IWithClient
{
    /// <summary>
    /// Gets or sets the requested scopes.
    /// </summary>
    /// <value>
    /// The requested scopes.
    /// </value>
    public HashSet<string> RequestedScopes { get; }

    /// <summary>
    /// The resources of the result.
    /// </summary>
    public Resources Resources { get; }
}
