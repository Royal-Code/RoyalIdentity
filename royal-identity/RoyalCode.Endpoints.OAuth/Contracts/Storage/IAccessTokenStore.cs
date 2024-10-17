using RoyalIdentity.Models.Tokens;

namespace RoyalIdentity.Contracts.Storage;

public interface IAccessTokenStore
{
    /// <summary>
    /// Stores the Access Token.
    /// </summary>
    /// <param name="code">The code.</param>
    /// <returns></returns>
    Task<string> StoreAsync(AccessToken code, CancellationToken ct);

    /// <summary>
    /// Gets the Access Token.
    /// </summary>
    /// <param name="code">The code.</param>
    /// <returns></returns>
    Task<AuthorizationCode> GetAsync(string jti, CancellationToken ct);

    /// <summary>
    /// Removes the Access Token.
    /// </summary>
    /// <param name="code">The code.</param>
    /// <returns></returns>
    Task RemoveAsync(string jti, CancellationToken ct);
}
