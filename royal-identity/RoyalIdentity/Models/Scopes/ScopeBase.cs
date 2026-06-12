using System.Diagnostics;

namespace RoyalIdentity.Models.Scopes;

/// <summary>
/// Models the common data of identity scopes, resource servers and scopes.
/// </summary>
[DebuggerDisplay("{DebuggerDisplay,nq}")]
public abstract class ScopeBase
{
    private string DebuggerDisplay => Name ?? $"{{{typeof(ScopeBase)}}}";

    protected ScopeBase(ScopeType type, ScopeVisibility visibility, string name, string displayName, string description)
    {
        Type = type;
        Visibility = visibility;
        Name = name;
        DisplayName = displayName;
        Description = description;
        ShowInDiscoveryDocument = visibility == ScopeVisibility.Public;
    }

    /// <summary>
    /// Indicates the type of the scope.
    /// </summary>
    public ScopeType Type { get; private set; }

    /// <summary>
    /// Indicates the visibility of the scope.
    /// </summary>
    public ScopeVisibility Visibility { get; set; }

    /// <summary>
    /// Indicates if this resource is enabled. Defaults to true.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// The unique name of the resource.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Display name of the resource.
    /// </summary>
    public string DisplayName { get; set; }

    /// <summary>
    /// Description of the resource.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Specifies whether this scope is shown in the discovery document. Defaults to true.
    /// </summary>
    public bool ShowInDiscoveryDocument { get; set; }
}
