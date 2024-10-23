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
    /// Gets or sets a value indicating whether the request was an OpenID Connect request.
    /// </summary>
    /// <value>
    /// <c>true</c> if the request was an OpenID Connect request; otherwise, <c>false</c>.
    /// </value>
    public bool IsOpenIdRequest { get; set; }

    /// <summary>
    /// The resources of the result.
    /// </summary>
    public Resources Resources { get; }

    /// <summary>
    /// Gets or sets a value indicating whether this instance is API resource request.
    /// </summary>
    /// <value>
    /// <c>true</c> if this instance is API resource request; otherwise, <c>false</c>.
    /// </value>
    public bool IsApiResourceRequest { get; set; }

    /// <summary>
    /// Gets or sets the type of the response.
    /// </summary>
    /// <value>
    /// The type of the response.
    /// </value>
    public HashSet<string> ResponseTypes { get; }
}
