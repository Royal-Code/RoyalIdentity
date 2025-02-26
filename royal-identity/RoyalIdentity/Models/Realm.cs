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
}
