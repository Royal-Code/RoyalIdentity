using RoyalIdentity.Models.Scopes;

namespace RoyalIdentity.Contexts.Withs;

public interface IWithResources : IWithClient
{
    public string? Scope { get; }

    /// <summary>
    /// The resources of the result.
    /// </summary>
    public RequestedResources Scopes { get; }

}
