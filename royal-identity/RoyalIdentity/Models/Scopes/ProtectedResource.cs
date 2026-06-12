using RoyalIdentity.Extensions;

namespace RoyalIdentity.Models.Scopes;

/// <summary>
/// Models a protected resource (RFC 8707 resource indicator / RFC 9728 metadata) exposed by a
/// <see cref="ResourceServer"/>. The OAuth <c>resource</c> parameter is matched against
/// <see cref="ResourceUri"/>. Availability derives from the parent <see cref="ScopeBase.Enabled"/>
/// of the owning <see cref="ResourceServer"/> (there is no own enabled flag).
/// </summary>
public class ProtectedResource
{
    public ProtectedResource(string resourceUri)
    {
        if (resourceUri.IsMissing())
            throw new ArgumentNullException(nameof(resourceUri));

        ResourceUri = resourceUri;
    }

    /// <summary>
    /// The absolute URI (without fragment) matched by the OAuth <c>resource</c> parameter and emitted as the audience.
    /// </summary>
    public string ResourceUri { get; set; }

    /// <summary>
    /// Whether this protected resource is published in discovery metadata. Defaults to true.
    /// </summary>
    public bool ShowInDiscoveryDocument { get; set; } = true;

    /// <summary>Human-readable name (RFC 9728 <c>resource_name</c>).</summary>
    public string? DisplayName { get; set; }

    /// <summary>RFC 9728 <c>resource_documentation</c>.</summary>
    public string? DocumentationUri { get; set; }

    /// <summary>RFC 9728 <c>resource_policy_uri</c>.</summary>
    public string? PolicyUri { get; set; }

    /// <summary>RFC 9728 <c>resource_tos_uri</c>.</summary>
    public string? TosUri { get; set; }
}
