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
    /// Updates the Refresh Token.
    /// </summary>
    /// <param name="token">The token.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns></returns>
    Task UpdateAsync(RefreshToken token, CancellationToken ct);

    /// <summary>
    /// Removes the Refresh Token.
    /// </summary>
    /// <param name="jti">The token it self of the token.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns></returns>
    Task RemoveAsync(string token, CancellationToken ct);
}
