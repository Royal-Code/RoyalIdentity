using RoyalIdentity.Models.Resources;

namespace RoyalIdentity.Models.Scopes;

/// <summary>
/// Model for Web API Resource Server.
/// </summary>
public class ResourceServer : ScopeBase
{
    /// <summary>
    /// Creates a new instance of <see cref="ResourceServer"/>.
    /// </summary>
    /// <param name="visibility">Visibility of the resource server.</param>
    /// <param name="name">The unique name of the resource server.</param>
    /// <param name="displayName">The display name of the resource server.</param>
    /// <param name="description">The description of the resource server.</param>
    public ResourceServer(
        ScopeVisibility visibility, 
        string name,
        string displayName, 
        string description) : base(ScopeType.ResourceServer, visibility, name, displayName, description)
    { }

    /// <summary>
    /// Indicates whether this API resource allows incoming scope requests. Defaults to true.
    /// </summary>
    public bool AllowScopeRequests { get; set; } = true;

    /// <summary>
    /// Models the resources this API resource server allows.
    /// </summary>
    public ICollection<ApiResource> Resources { get; set; } = [];

    /// <summary>
    /// Models the identity scopes this API resource server allows.
    /// </summary>
    public ICollection<IdentityScope> IdentityScopes { get; set; } = [];
}
