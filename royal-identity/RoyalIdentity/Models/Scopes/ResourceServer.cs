using RoyalIdentity.Extensions;
using System.Diagnostics;

namespace RoyalIdentity.Models.Scopes;

/// <summary>
/// Models a resource server (a Web API) that exposes protected <see cref="Scope"/>s.
/// </summary>
[DebuggerDisplay("{" + nameof(DebuggerDisplay) + ",nq}")]
public class ResourceServer : ScopeBase
{
    private string DebuggerDisplay => Name ?? $"{{{typeof(ResourceServer)}}}";

    /// <summary>
    /// Initializes a new instance of the <see cref="ResourceServer"/> class.
    /// </summary>
    public ResourceServer(
        ScopeVisibility visibility,
        string name,
        string displayName,
        string description)
        : base(ScopeType.ResourceServer, visibility, name, displayName, description)
    {
        if (name.IsMissing()) throw new ArgumentNullException(nameof(name));
    }

    /// <summary>
    /// Copy constructor. Produces an independent instance with copied collections
    /// (new collections sharing the same scope/secret element references).
    /// </summary>
    public ResourceServer(ResourceServer other)
        : this(other.Visibility, other.Name, other.DisplayName, other.Description)
    {
        Enabled = other.Enabled;
        ShowInDiscoveryDocument = other.ShowInDiscoveryDocument;
        Audience = other.Audience;
        AllowScopeRequests = other.AllowScopeRequests;
        Scopes = [.. other.Scopes];
        Secrets = [.. other.Secrets];
        AllowedAccessTokenSigningAlgorithms = [.. other.AllowedAccessTokenSigningAlgorithms];
    }

    /// <summary>
    /// The audience added to access tokens for scopes of this resource server.
    /// When empty, <see cref="ScopeBase.Name"/> is used.
    /// </summary>
    public string? Audience { get; set; }

    /// <summary>
    /// Indicates whether this resource server allows incoming scope requests. Defaults to true.
    /// </summary>
    public bool AllowScopeRequests { get; set; } = true;

    /// <summary>
    /// The scopes exposed by this resource server.
    /// </summary>
    public ICollection<Scope> Scopes { get; set; } = [];

    /// <summary>
    /// Secrets used by the resource server (e.g. to authenticate at the introspection endpoint).
    /// </summary>
    public ICollection<ClientSecret> Secrets { get; set; } = [];

    /// <summary>
    /// Signing algorithms accepted by this resource server for access tokens.
    /// If empty, the resource server imposes no restriction.
    /// </summary>
    public HashSet<string> AllowedAccessTokenSigningAlgorithms { get; set; } = [];

    /// <summary>
    /// Gets the effective audience (<see cref="Audience"/> when set, otherwise <see cref="ScopeBase.Name"/>).
    /// </summary>
    public string GetAudience() => Audience.IsPresent() ? Audience : Name;
}
