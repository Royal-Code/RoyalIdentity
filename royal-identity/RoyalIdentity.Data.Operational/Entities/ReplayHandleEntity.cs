namespace RoyalIdentity.Data.Operational.Entities;

/// <summary>
/// Replay-protection row (table <c>replay_handles</c>, plan-replay-protection DF16): a handle that has been
/// presented once and must never be accepted again while the record is retained — today the <c>jti</c> of a
/// <c>private_key_jwt</c> client assertion.
/// <para>
/// The row <b>is</b> the record: there is no payload, nothing is ever read back from it and no column exists that
/// something else has to interpret. Its whole identity is the key, and the key is the uniqueness that decides
/// replay: the second insert of the same identity violates it, and that violation is the answer.
/// </para>
/// <para>
/// It carries no <c>created_at_utc</c>. Every other table in this family has one because something reads it —
/// a lifetime, a tolerance, a materialized model. Nothing would read this one.
/// </para>
/// </summary>
public class ReplayHandleEntity
{
    /// <summary>The realm the handle was presented to. A logical link by value (plan DF6).</summary>
    public required string RealmId { get; set; }

    /// <summary>
    /// The issuer that minted the handle — the validated <c>client_id</c> for a client assertion. Part of the
    /// identity so one client can never occupy another client's handle.
    /// </summary>
    public required string Issuer { get; set; }

    /// <summary>The kind of artifact protected, so unrelated single-use artifacts never share a namespace.</summary>
    public required string Purpose { get; set; }

    /// <summary>
    /// Digest of the handle; the raw value is never persisted (plan-replay-protection DF4). Realm, issuer and
    /// purpose are columns of their own and deliberately not folded into it.
    /// </summary>
    public required string HandleDigest { get; set; }

    /// <summary>
    /// The instant after which the record no longer protects anything, because the artifact it stands for can no
    /// longer be accepted. Cleanup removes rows strictly past it; nothing on the write path ever reads it
    /// (plan-replay-protection DF8).
    /// </summary>
    public DateTime ExpiresAtUtc { get; set; }
}
