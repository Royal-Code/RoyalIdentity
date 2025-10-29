using Microsoft.AspNetCore.DataProtection;
using RoyalIdentity.Extensions;
using RoyalIdentity.Models.Resources;
using System.Diagnostics;

namespace RoyalIdentity.Models.Scopes;

/// <summary>
/// Models a web API resource.
/// </summary>
[DebuggerDisplay("{" + nameof(DebuggerDisplay) + ",nq}")]
public class ApiResource : ScopeBase
{
    private string DebuggerDisplay => Name ?? $"{{{typeof(ApiResource)}}}";

    /// <summary>
    /// Initializes a new instance of the <see cref="ApiResource"/> class.
    /// </summary>
    /// <param name="visibility">The visibility.</param>
    /// <param name="name">The name.</param>
    /// <param name="displayName">The display name.</param>
    /// <param name="description">The description.</param>
    /// <param name="userClaims">List of associated user claims that should be included when this resource is requested.</param>
    /// <exception cref="System.ArgumentNullException">name</exception>
    public ApiResource(
        ScopeVisibility visibility,
        string name,
        string displayName,
        string description)
        : base (ScopeType.ApiResource, visibility, name, displayName, description)
    {
        if (name.IsMissing()) throw new ArgumentNullException(nameof(name));
    }

    /// <summary>
    /// Indicates whether this API resource allows incoming scope requests. Defaults to true.
    /// </summary>
    public bool AllowScopeRequests { get; set; } = true;

    /// <summary>
    /// The API secret is used for the introspection endpoint. The API can authenticate with introspection using the API name and secret.
    /// </summary>
    [Obsolete("This property must be moved to client")]
    public ICollection<Secret> ApiSecrets { get; set; } = new HashSet<Secret>();

    /// <summary>
    /// Models the scopes this API scopes allows.
    /// </summary>
    public ICollection<ApiScope> Scopes { get; set; } = [];

    /// <summary>
    /// Signing algorithm for access token. If empty, will use the server default signing algorithm.
    /// </summary>
    [Obsolete("This property must be moved to client")]
    public HashSet<string> AllowedAccessTokenSigningAlgorithms { get; set; } = [];
}
