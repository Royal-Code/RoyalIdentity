namespace RoyalIdentity.Data.Operational.Entities;

/// <summary>
/// User consent row (table <c>consents</c>): identity is the real composite key
/// <c>(RealmId, SubjectId, ClientId)</c> — never a concatenated string — and writes are an upsert, so
/// concurrent writers never produce two rows (plan DF7/DF14). The consented scopes live in the versioned,
/// protected payload; only what lookup and cleanup need stays relational.
/// </summary>
public class ConsentEntity
{
    /// <summary>The realm this consent belongs to. A logical link by value (plan DF6).</summary>
    public required string RealmId { get; set; }

    public required string SubjectId { get; set; }

    /// <summary>The client the consent was granted to. A logical link by value (plan DF6).</summary>
    public required string ClientId { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    /// <summary>
    /// Absolute expiration, or <c>null</c> when the consent does not expire. A consent without expiration is
    /// never removed by cleanup — only by explicit removal or realm purge (plan DF17).
    /// </summary>
    public DateTime? ExpiresAtUtc { get; set; }

    public int PayloadVersion { get; set; }

    /// <summary>The versioned payload envelope holding the consented scopes (plan DF30).</summary>
    public required string ProtectedPayload { get; set; }
}
