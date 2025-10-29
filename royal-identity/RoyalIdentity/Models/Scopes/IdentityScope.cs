using RoyalIdentity.Extensions;
using RoyalIdentity.Models.Resources;
using System.Diagnostics;

namespace RoyalIdentity.Models.Scopes;

/// <summary>
/// Models a user identity resource.
/// </summary>
[DebuggerDisplay("{" + nameof(DebuggerDisplay) + ",nq}")]
public class IdentityScope : ScopeBase
{
    private string DebuggerDisplay => Name ?? $"{{{typeof(IdentityScope)}}}";

    /// <summary>
    /// Initializes a new instance of the <see cref="IdentityScope"/> class.
    /// </summary>
    /// <param name="visibility">The visibility.</param>
    /// <param name="name">The name.</param>
    /// <param name="displayName">The display name.</param>
    /// <param name="userClaims">List of associated user claims that should be included when this resource is requested.</param>
    /// <param name="description">The description.</param>
    /// <exception cref="System.ArgumentNullException">name</exception>
    /// <exception cref="System.ArgumentException">Must provide at least one claim type - claimTypes</exception>
    public IdentityScope(
        ScopeVisibility visibility, 
        string name, 
        string displayName, 
        string description,
        IEnumerable<string> userClaims)
        : base (ScopeType.Identity, visibility, name, displayName, displayName)
    {
        if (name.IsMissing()) 
            throw new ArgumentNullException(nameof(name));
        if (userClaims.IsNullOrEmpty()) 
            throw new ArgumentException("Must provide at least one claim type", nameof(userClaims));

        foreach (var type in userClaims)
        {
            UserClaims.Add(type);
        }
    }

    /// <summary>
    /// Specifies whether the user can de-select the scope on the consent screen 
    /// (if the consent screen wants to implement such a feature). 
    /// Defaults to false.
    /// </summary>
    public bool Required { get; set; } = false;

    /// <summary>
    /// Specifies whether the consent screen will emphasize this scope 
    /// (if the consent screen wants to implement such a feature). 
    /// Use this setting for sensitive or important scopes. Defaults to false.
    /// </summary>
    public bool Emphasize { get; set; } = false;

    /// <summary>
    /// <para>
    ///     List of associated user claims that should be included when this resource is requested.
    /// </para>
    /// <para>
    ///     This is typically used to specify which claims should be included in identity tokens or access tokens
    /// </para>
    /// </summary>
    public HashSet<string> UserClaims { get; set; } = [];
}
