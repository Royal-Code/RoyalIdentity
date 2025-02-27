using RoyalIdentity.Options;

namespace RoyalIdentity.Models;

/// <summary>
/// <para>
///     Represents a realm in the authorization server.
/// </para>
/// <para>
///     A realm is a domain for clients, scopes, configurations, users, roles, groups and functions. 
///     Clients and users belong to a realm and log in to it.
/// </para>
/// </summary>
public class Realm
{
    /// <summary>
    /// Creates a new instance of <see cref="Realm"/>.
    /// </summary>
    /// <param name="id">Optional, the unique identifier of the realm. When not set, a new GUID is generated.</param>
    /// <param name="domain">The realm domain. Example: "example.com"</param>
    /// <param name="path">The realm Path. Used to identify the realm in the URL.</param>
    /// <param name="displayName">The realm display name.</param>
    /// <param name="internal">Determines if the realm is internal, managed by the server.</param>
    /// <param name="options">The options for the realm.</param>
    public Realm(string? id, string domain, string path, string displayName, bool @internal, RealmOptions options)
    {
        Id = id ?? Guid.NewGuid().ToString();
        Domain = domain;
        Path = path;
        DisplayName = displayName;
        Enabled = true;
        Internal = @internal;
        Options = options;
    }

    /// <summary>
    /// The unique identifier of the realm.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// The realm domain.
    /// Example: "example.com"
    /// </summary>
    public string Domain { get; set; }

    /// <summary>
    /// The realm Path. Used to identify the realm in the URL.
    /// </summary>
    public string Path { get; set; }

    /// <summary>
    /// The realm display name.
    /// </summary>
    public string DisplayName { get; set; }

    /// <summary>
    /// Determines if the realm is enabled.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Determines if the realm is internal, managed by the server.
    /// When is internal, the realm cannot be deleted or have its domain changed.
    /// </summary>
    public bool Internal { get; set; }

    /// <summary>
    /// The options for the realm.
    /// </summary>
    public RealmOptions Options { get; set; }
}
