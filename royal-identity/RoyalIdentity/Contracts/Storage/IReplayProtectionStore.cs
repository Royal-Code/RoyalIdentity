namespace RoyalIdentity.Contracts.Storage;

/// <summary>
/// <para>
///     Single-use protection for handles that must never be accepted twice — today the <c>jti</c> of a
///     <c>private_key_jwt</c> client assertion, which RFC 7519 §4.1.7 requires to be assigned so that collision
///     is negligible and OpenID Connect Core §9 requires to be used only once.
/// </para>
/// <para>
///     There is deliberately no "exists" operation to pair with an "add". Checking and then writing is two
///     operations, and two concurrent callers both pass the check before either writes — which is exactly the
///     replay this contract exists to stop. The single operation below is the whole contract and must be atomic
///     in every implementation.
/// </para>
/// </summary>
public interface IReplayProtectionStore
{
    /// <summary>
    /// Registers <paramref name="handle"/> if, and only if, it is not already registered under the same realm,
    /// issuer and purpose.
    /// </summary>
    /// <param name="realmId">The realm the handle belongs to. Records never cross realms.</param>
    /// <param name="issuer">
    /// The issuer that minted the handle — for client assertions, the validated <c>client_id</c>. Records never
    /// cross issuers, so one client can never block another client's handle.
    /// </param>
    /// <param name="purpose">The kind of artifact being protected, so unrelated uses never share a namespace.</param>
    /// <param name="handle">
    /// The value that must be single-use. Implementations that <b>persist</b> records store a digest of it and
    /// never the literal value; an implementation holding records only in process memory may keep it as-is, since
    /// the value already lives in memory for the duration of the request that carried it.
    /// </param>
    /// <param name="expiration">
    /// The instant after which the record may be discarded. It must cover at least the artifact's own validity
    /// plus the clock skew the caller tolerates; otherwise the record would expire while the artifact is still
    /// acceptable and reopen the replay window.
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// <para>
    ///     <c>true</c> when this call registered the handle and the caller may proceed; <c>false</c> when it was
    ///     already registered — a replay.
    /// </para>
    /// <para>
    ///     Implementations never consult the expiration of an existing record: while a record is retained, a
    ///     conflict answers replay. That keeps correctness independent of both the clock and the pruning of
    ///     expired records. Implementations also never answer <c>true</c> on infrastructure failure — they
    ///     throw, so the caller fails closed.
    /// </para>
    /// </returns>
    Task<bool> TryAddAsync(
        string realmId,
        string issuer,
        string purpose,
        string handle,
        DateTimeOffset expiration,
        CancellationToken ct);
}
