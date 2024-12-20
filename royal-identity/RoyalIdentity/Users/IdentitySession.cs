namespace RoyalIdentity.Users;

/// <summary>
/// Represents an identity session.
/// A session is created when the user sign in. with their credentials or another authentication method.
/// </summary>
public class IdentitySession
{
    /// <summary>
    /// The unique identifier for the session.
    /// This is used to identify the session in the database and in authentication processes.
    /// It is the JWT Session ID (sid) claim value.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// The user that owns the session.
    /// </summary>
    public required IdentityUser User { get; init; }

    /// <summary>
    /// The authentication method reference (amr) claim value.
    /// </summary>
    public required string Amr { get; init; }

    /// <summary>
    /// The clients (client_id) that the user has sign in into during their session.
    /// </summary>
    public IList<string> Clients { get; init; } = [];

    /// <summary>
    /// Indicates if the session is active or not.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Date and time when the session was created.
    /// </summary>
    public required DateTime StartedAt { get; init; }
}