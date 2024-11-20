using RoyalIdentity.Models.Tokens;

namespace RoyalIdentity.Contracts.Storage;

public interface IAccessTokenStore
{
    /// <summary>
    /// Stores the Access Token.
    /// </summary>
    /// <param name="token">The token.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns></returns>
    Task<string> StoreAsync(AccessToken token, CancellationToken ct);

    /// <summary>
    /// Gets the Access Token.
    /// </summary>
    /// <param name="jti">The id of the token.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns></returns>
    Task<AccessToken?> GetAsync(string jti, CancellationToken ct);

    /// <summary>
    /// Removes the Access Token.
    /// </summary>
    /// <param name="jti">The id of the token.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns></returns>
    Task RemoveAsync(string jti, CancellationToken ct);

    /// <summary>
    /// Removes the reference tokens.
    /// </summary>
    /// <param name="subjectId">The subject identifier.</param>
    /// <param name="clientId">The client identifier.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns></returns>
    Task RemoveReferenceTokensAsync(string subjectId, string clientId, CancellationToken ct);
}
