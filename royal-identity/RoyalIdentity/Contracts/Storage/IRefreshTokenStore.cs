using RoyalIdentity.Models.Tokens;

namespace RoyalIdentity.Contracts.Storage;

public interface IRefreshTokenStore
{
    /// <summary>
    /// Stores the Refresh Token.
    /// </summary>
    /// <param name="token">The token.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns></returns>
    Task StoreAsync(RefreshToken token, CancellationToken ct);

    /// <summary>
    /// Gets the Refresh Token.
    /// </summary>
    /// <param name="token">The token it self of the token.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns></returns>
    Task<RefreshToken?> GetAsync(string token, CancellationToken ct);

    /// <summary>
    /// Marks the token as consumed if it is still unconsumed at the expected state version. Exactly one
    /// concurrent caller can observe a successful transition. The returned state is rematerialized so the
    /// caller can apply the separate post-consumption tolerance policy.
    /// </summary>
    /// <param name="token">The raw refresh-token handle.</param>
    /// <param name="expectedStateVersion">The version observed when the token was materialized.</param>
    /// <param name="consumedAt">The consumption timestamp.</param>
    /// <param name="ct">The cancellation token.</param>
    Task<RefreshTokenTransition> TryConsumeAsync(
        string token, int expectedStateVersion, DateTime consumedAt, CancellationToken ct);

    /// <summary>
    /// Persists a later change to a reusable token only at the expected state version, so a concurrent writer
    /// cannot be lost.
    /// </summary>
    /// <param name="token">The token to persist.</param>
    /// <param name="expectedStateVersion">The version observed when the token was materialized.</param>
    /// <param name="ct">The cancellation token.</param>
    Task<RefreshTokenTransition> TryUpdateAsync(
        RefreshToken token, int expectedStateVersion, CancellationToken ct);

    /// <summary>
    /// Removes the Refresh Token.
    /// </summary>
    /// <param name="jti">The token it self of the token.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns></returns>
    Task RemoveAsync(string token, CancellationToken ct);

    /// <summary>
    /// Removes all refresh tokens of a subject (active-revocation seam — Q13). Idempotent. Returns the number removed.
    /// </summary>
    /// <param name="subjectId">The subject whose refresh tokens are removed.</param>
    /// <param name="ct">The cancellation token.</param>
    Task<int> RemoveBySubjectAsync(string subjectId, CancellationToken ct);
}
