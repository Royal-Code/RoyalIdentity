using RoyalIdentity.Extensions;
using RoyalIdentity.Models.Resources;
using System.Diagnostics;

namespace RoyalIdentity.Models.Scopes;

/// <summary>
/// Models access to an API scope
/// </summary>
[DebuggerDisplay("{" + nameof(DebuggerDisplay) + ",nq}")]
public class ApiScope : ScopeBase
{
    private string DebuggerDisplay => Name ?? $"{{{typeof(ApiScope)}}}";

    /// <summary>
    /// Initializes a new instance of the <see cref="ApiScope"/> class.
    /// </summary>
    /// <param name="visibility">The visibility.</param>
    /// <param name="name">The name.</param>
    /// <param name="displayName">The display name.</param>
    /// <param name="description">The description.</param>
    /// <param name="userClaims">List of associated user claims that should be included when this resource is requested.</param>
    /// <exception cref="System.ArgumentNullException">name</exception>
    public ApiScope(
        ScopeVisibility visibility,
        string name, 
        string displayName, 
        string description)
        : base (ScopeType.ApiScope, visibility, name, displayName, description)
    {
        if (name.IsMissing()) throw new ArgumentNullException(nameof(name));
    }

    /// <summary>
    /// Specifies whether the user can de-select the scope on the consent screen. Defaults to false.
    /// </summary>
    public bool Required { get; set; } = false;

    /// <summary>
    /// Specifies whether the consent screen will emphasize this scope. Use this setting for sensitive or important scopes. Defaults to false.
    /// </summary>
    public bool Emphasize { get; set; } = false;
}
