using RoyalIdentity.Extensions;
using System.Diagnostics;

namespace RoyalIdentity.Models.Scopes;

/// <summary>
/// Models a scope: an operation exposed by a <see cref="ResourceServer"/>.
/// </summary>
[DebuggerDisplay("{" + nameof(DebuggerDisplay) + ",nq}")]
public class Scope : ScopeBase
{
    private string DebuggerDisplay => Name ?? $"{{{typeof(Scope)}}}";

    /// <summary>
    /// Initializes a new instance of the <see cref="Scope"/> class.
    /// </summary>
    public Scope(
        ScopeVisibility visibility,
        string name,
        string displayName,
        string description)
        : base(ScopeType.Scope, visibility, name, displayName, description)
    {
        if (name.IsMissing()) throw new ArgumentNullException(nameof(name));
    }

    /// <summary>
    /// Specifies whether the user can de-select the scope on the consent screen. Defaults to false.
    /// </summary>
    public bool Required { get; set; } = false;

    /// <summary>
    /// Specifies whether the consent screen will emphasize this scope. Defaults to false.
    /// </summary>
    public bool Emphasize { get; set; } = false;
}
