using RoyalIdentity.Models.Resources;

namespace RoyalIdentity.Contexts.Withs;

public interface IWithResources : IWithClient
{
    public string? Scope { get; }

    /// <summary>
    /// The resources of the result.
    /// </summary>
    public RequestedScopes Scopes { get; }

    /// <summary>
    /// Determines that the resources have been loaded and are valid for the client.
    /// </summary>
    public void ResourcesValidated();

    /// <summary>
    /// Ensures that the resources have been validated.
    /// </summary>
    public void AssertResourcesValidated();
}
