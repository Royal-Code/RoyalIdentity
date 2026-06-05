using RoyalIdentity.Models.Resources;

namespace RoyalIdentity.Contexts.Withs;

public interface IWithResources : IWithClient
{
    public string? Scope { get; }

    /// <summary>
    /// The resources of the result.
    /// </summary>
    public RequestedScopes Scopes { get; }

}
