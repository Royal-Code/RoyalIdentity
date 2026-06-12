using RoyalIdentity.Extensions;
using System.Diagnostics;

namespace RoyalIdentity.Models.Scopes;

/// <summary>
/// Models a user identity scope (the claims sent to the client).
/// </summary>
[DebuggerDisplay("{" + nameof(DebuggerDisplay) + ",nq}")]
public class IdentityScope : ScopeBase
{
    private string DebuggerDisplay => Name ?? $"{{{typeof(IdentityScope)}}}";

    /// <summary>
    /// Initializes a new instance of the <see cref="IdentityScope"/> class.
    /// </summary>
    public IdentityScope(
        ScopeVisibility visibility,
        string name,
        string displayName,
        string description,
        IEnumerable<string> userClaims)
        : base(ScopeType.Identity, visibility, name, displayName, displayName)
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
    /// Specifies whether the user can de-select the scope on the consent screen. Defaults to false.
    /// </summary>
    public bool Required { get; set; } = false;

    /// <summary>
    /// Specifies whether the consent screen will emphasize this scope. Defaults to false.
    /// </summary>
    public bool Emphasize { get; set; } = false;

    /// <summary>
    /// List of associated user claims that should be included when this scope is requested.
    /// </summary>
    public HashSet<string> UserClaims { get; set; } = [];
}
