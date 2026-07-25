namespace RoyalIdentity.Data.Operational.Entities;

/// <summary>
/// A client the subject signed into during a session (table <c>user_session_clients</c>). Deduplication is the
/// composite primary key itself, so concurrent records of the same client can never produce two rows
/// (plan DF15). This is the only foreign key inside the Operational family: structural ownership with a shared
/// lifecycle, within the same realm (plan DF35).
/// </summary>
public class UserSessionClientEntity
{
    public required string RealmId { get; set; }

    public required string SessionId { get; set; }

    /// <summary>The client. A logical link by value to the Configuration family (plan DF6).</summary>
    public required string ClientId { get; set; }

    public DateTime FirstSeenAtUtc { get; set; }

    public DateTime LastSeenAtUtc { get; set; }
}
